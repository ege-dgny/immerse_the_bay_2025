# Afference SDK Flow Analysis: Calibration Menu → BLE Connection

## Overview
This document explains the complete flow from the calibration menu to BLE device connection in the Afference Unity SDK, and compares it to your custom implementation.

---

## 📋 Complete Flow: Afference SDK

### **Stage 1: Initialization (HapticManager.Awake)**

**File:** `HapticManager.cs` (Lines 53-67)

```csharp
private void Awake()
{
    // Singleton pattern
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    DontDestroyOnLoad(gameObject);
    Instance = this;

    Application.runInBackground = true;
    Screen.sleepTimeout = SleepTimeout.NeverSleep;
    new CopyAssets().CopyAllAssets();  // Copies StreamingAssets to persistentDataPath
    Application.targetFrameRate = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
    SetCommType("ble");  // Sets commType = "ble" automatically
#endif
}
```

**Key Points:**
- ✅ Automatically sets `commType = "ble"` on Android
- ✅ Copies assets (Users, Encoders, BodyLocations) from StreamingAssets
- ✅ Sets up singleton pattern
- ❌ **Does NOT set device ID** - must be set manually before connection

---

### **Stage 2: User Selection/Creation**

#### **Option A: Existing User (ExistingUserDropdown.cs)**

**File:** `ExistingUserDropdown.cs` (Lines 137-166)

**Flow:**
1. User selects from dropdown → `selectedUser` set
2. User clicks "Continue" → `LoadUser()` called
3. **Load user:**
   ```csharp
   HapticManager.Instance.LoadUser(selectedUser);
   ```
4. **Request permissions:**
   ```csharp
   bool permOK = await HapticManager.Instance.EnsureBlePermissionsAsync();
   ```
5. **Wait for focus:**
   ```csharp
   await HapticManager.Instance.WaitForAndroidFocusAndStabilityAsync(350);
   ```
6. **Connect:**
   ```csharp
   bool ok = await HapticManager.Instance.ConnectCurrentUserAsync();
   ```

#### **Option B: New User (NewUserForm.cs)**

**File:** `NewUserForm.cs` (Lines 93-120)

**Flow:**
1. User enters name → validates
2. User clicks "Create" → `HandleCreateClicked()`
3. **Create user file:**
   ```csharp
   User CreatedUser = new User { Name = new User.NameFormat(first, middle, last) };
   CreatedUser.SaveUserData(targetPath);
   ```
4. **Load user:**
   ```csharp
   HapticManager.Instance.LoadUser(fileBase);
   ```
5. **Request permissions:**
   ```csharp
   bool permOK = await HapticManager.Instance.EnsureBlePermissionsAsync();
   ```
6. **Wait for focus:**
   ```csharp
   await HapticManager.Instance.WaitForAndroidFocusAndStabilityAsync(350);
   ```
7. **Connect:**
   ```csharp
   bool ok = await HapticManager.Instance.ConnectCurrentUserAsync();
   ```

**⚠️ Critical Note:** Neither script sets the device ID! The device must be set elsewhere or defaults must exist.

---

### **Stage 3: BLE Permissions (HapticManager)**

**File:** `HapticManager.cs` (Lines 89-123)

```csharp
public async Task<bool> EnsureBlePermissionsAsync(int timeoutMs = 10000)
{
    // Check Android SDK version
    int sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
    blePermissions = (sdk >= 31)
        ? new string[] { "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT" }
        : new string[] { Permission.FineLocation };

    // Check if already granted
    bool allGranted = true;
    foreach (var p in blePermissions)
        if (!Permission.HasUserAuthorizedPermission(p)) { allGranted = false; break; }
    if (allGranted) return true;

    // Request permissions with TaskCompletionSource pattern
    blePermissionTcs = new TaskCompletionSource<bool>();
    var pcb = new PermissionCallbacks();
    pcb.PermissionGranted += _ => ResolvePermissions();
    pcb.PermissionDenied += _ => ResolvePermissions();
    pcb.PermissionDeniedAndDontAskAgain += _ => ResolvePermissions();

    Permission.RequestUserPermissions(blePermissions, pcb);

    // Timeout fallback
    using var cts = new CancellationTokenSource(timeoutMs);
    cts.Token.Register(() => {
        if (blePermissionTcs != null && !blePermissionTcs.Task.IsCompleted)
            blePermissionTcs.TrySetResult(false);
    });

    return await blePermissionTcs.Task;
}
```

**Key Points:**
- ✅ Uses `TaskCompletionSource` for async permission handling
- ✅ Handles Android 12+ (SDK 31+) vs older versions
- ✅ Has timeout fallback (10 seconds)
- ✅ Uses callback pattern to resolve permissions

---

### **Stage 4: Android Focus Stability**

**File:** `HapticManager.cs` (Lines 139-147)

```csharp
public async Task WaitForAndroidFocusAndStabilityAsync(int postFocusDelayMs = 300)
{
    // Wait until the permission dialog is gone and app is foregrounded again.
    while (!Application.isFocused)
        await System.Threading.Tasks.Task.Yield();

    // Give the platform a breath to settle Bluetooth state (Gatt, callbacks).
    await System.Threading.Tasks.Task.Delay(postFocusDelayMs);
}
```

**Purpose:** Ensures app has focus after permission dialog and gives BLE stack time to settle.

---

### **Stage 5: BLE Connection (HapticManager)**

**File:** `HapticManager.cs` (Lines 154-268)

**Flow:**

```csharp
public async Task<bool> ConnectCurrentUserAsync(
    int maxAttempts = 5,
    float initialDelaySeconds = 1.0f)
{
    // Validation
    if (currentUser == null) { Debug.LogError("No user loaded."); return false; }
    if (string.IsNullOrEmpty(commType)) { Debug.LogError("commType not set"); return false; }
    if (string.IsNullOrEmpty(port)) { Debug.LogError("port/device not set"); return false; }

    // Cancel any existing connection attempts
    _cts?.Cancel();
    _cts = new CancellationTokenSource();
    var token = _cts.Token;

    status = "Attempting connection...";
    Exception lastError = null;

#if UNITY_ANDROID && !UNITY_EDITOR
    AfferenceRingAndroidTransport native = null;
    Transport transport = null;

    // Initial transport creation
    try
    {
        native = new AfferenceRingAndroidTransport(openTimeoutMs: 12_000);
        transport = new Transport(native, commType, port);
        await Task.Delay(600); // small settle; CCCD/MTU complete
    }
    catch (Exception ex)
    {
        Debug.LogError($"[HapticManager] Initial transport open failed: {ex}");
        try { transport?.Dispose(); } catch { }
        return false;
    }

    // Retry loop (up to 5 attempts)
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            // Rebuild transport if needed
            if (native == null || transport == null)
            {
                try { transport?.Dispose(); } catch { }
                native = new AfferenceRingAndroidTransport(openTimeoutMs: 12_000);
                transport = new Transport(native, commType, port);
                await Task.Delay(600);
            }

            // Create ring object
            var ring = new AfferenceRing(transport, currentUser, digit: 2);
            
            // Create session (sets up encoders, calibration data, etc.)
            CreateSession(currentUser, ring);

            status = "Connected!";
            return true;
        }
        catch (OperationCanceledException)
        {
            status = "Connection canceled";
            return false;
        }
        catch (Exception ex)
        {
            lastError = ex;
            Debug.LogError($"[HapticManager] Attempt {attempt}/{maxAttempts} failed: {ex}");

            // Cleanup and retry with exponential backoff
            try { transport?.Dispose(); } catch { }
            native = null;
            transport = null;

            if (attempt < maxAttempts)
            {
                float wait = initialDelaySeconds * Mathf.Pow(1.5f, attempt - 1);
                status = $"Retrying... ({attempt + 1}/{maxAttempts})";
                try { await Task.Delay(TimeSpan.FromSeconds(wait), token); }
                catch (OperationCanceledException) { return false; }
            }
        }
    }

    // Final cleanup on failure
    try { transport?.Dispose(); } catch { }
    if (lastError != null) Debug.LogException(lastError);
    status = "Could not connect to the Afference Ring.\nPlease restart the app.";
    return false;
#endif
}
```

**Key Points:**
- ✅ Creates `AfferenceRingAndroidTransport` with 12-second timeout
- ✅ Wraps in `Transport` class with `commType` and `port`
- ✅ Waits 600ms for CCCD/MTU to complete
- ✅ Creates `AfferenceRing` object with transport and user
- ✅ Calls `CreateSession()` which:
  - Creates `HapticFactory` and `HapticSession`
  - Sets up `lateralBounder` and `medialBounder` (PulseParamManager)
  - Creates calibration encoders (lateral/medial)
  - Loads all quality encoders from JSON files
  - Activates encoders
- ✅ Retry logic with exponential backoff (1s, 1.5s, 2.25s, 3.375s, 5.0625s)
- ✅ Proper cleanup on failure

---

### **Stage 6: Session Creation (HapticManager.CreateSession)**

**File:** `HapticManager.cs` (Lines 270-296)

```csharp
public void CreateSession(User user, AfferenceRing ring)
{
    if (user == null) { Debug.LogError("No User!"); return; }
    if (ring == null) { Debug.LogError("No Ring!"); return; }

    // Create haptic factory and session
    hf = new HapticFactory();
    hs = hf.CreateSession(user, ring);

    // Get pulse parameter managers for lateral/medial
    var product = Products.AfferenceRing;
    lateralBounder = user.GetPulseManager(product, new BodyLocation("D2", "lateral"));
    medialBounder = user.GetPulseManager(product, new BodyLocation("D2", "medial"));

    // Create calibration encoders
    calibrationEncoderLateral = hs.CreateEncoder(EncoderModel.DirectStim,
        new BodyLocation("D2", "lateral"), new BodyLocation("D2", "dorsal"));
    calibrationEncoderMedial = hs.CreateEncoder(EncoderModel.DirectStim,
        new BodyLocation("D2", "medial"), new BodyLocation("D2", "dorsal"));

    calibrationEncoderLateral.BuildEncoder(EncoderModel.DirectStim);
    calibrationEncoderMedial.BuildEncoder(EncoderModel.DirectStim);

    // Add to quality encoders list
    qualityEncoders ??= new List<IQualityEncoder>();
    qualityEncoders.Clear();
    qualityEncoders.Add(calibrationEncoderLateral);
    qualityEncoders.Add(calibrationEncoderMedial);

    // Load all encoders from JSON files
    LoadAllEncoders();
}
```

**Key Points:**
- Creates haptic session with user and ring
- Sets up pulse parameter managers (for calibration)
- Creates calibration encoders (DirectStim model)
- Loads additional quality encoders from JSON files
- **Does NOT activate encoders** - that happens in calibration or manually

---

### **Stage 7: Calibration (Calibration.cs)**

**File:** `Calibration.cs` (Lines 70-82)

**When calibration panel opens:**

```csharp
private void OnEnable()
{
    // Start stimulation if not active
    if (!HapticManager.Instance.stimActive) { 
        HapticManager.Instance.ToggleStim(); 
    }
    
    // Activate calibration encoders (indices 0 and 1)
    HapticManager.Instance.ActivateEncoders(0, 1);
    Debug.Log("Activating calibration encoder");

    // Set calibration parameters
    calFrequency = tapCalFrequency;  // 3 Hz
    PA_CalibrationPulseDuration = tapPA_CalibrationDuration;  // 1 second
    PW_CalibrationPulseDuration = tapPW_CalibrationDuration;  // 1 second
}
```

**Calibration Process:**
1. User adjusts PA threshold → sends test pulse via `calibrationEncoderLateral` or `calibrationEncoderMedial`
2. User adjusts PA maximum → sends test pulse
3. User adjusts PW threshold → sends test pulse
4. User adjusts PW maximum → sends test pulse
5. Repeat for medial side
6. On completion → saves user data and activates quality encoders (indices 2, 3)

**Key Methods:**
- `UpdateUserIntensityDiscrete(float direction)` - Adjusts PA/PW values
- `TestUserPAThreshold()`, `TestUserPAMaximum()`, etc. - Sends test pulses
- `CalibrationSettingConfirmation()` - Moves to next step, saves on completion

---

## 🔄 Comparison: Afference SDK vs Your Implementation

### **Your Implementation (AfferenceRingPairing.cs)**

```csharp
public IEnumerator InitializeAndConnect()
{
    // 1. Set device ID
    HapticManager.Instance.SetDevice(deviceId);  // ✅ Explicit
    
    // 2. Set comm type
    HapticManager.Instance.SetCommType("ble");  // ✅ Explicit
    
    // 3. Load user
    HapticManager.Instance.LoadUser(userName);  // ✅ Explicit
    
    // 4. Wait frame
    yield return null;
    
    // 5. Request permissions
    Task<bool> permissionTask = HapticManager.Instance.EnsureBlePermissionsAsync();
    yield return new WaitUntil(() => permissionTask.IsCompleted);
    
    // 6. Wait for focus
    Task focusTask = HapticManager.Instance.WaitForAndroidFocusAndStabilityAsync();
    yield return new WaitUntil(() => focusTask.IsCompleted);
    
    // 7. Wait for FlexGlove (YOUR ADDITION)
    // ... wait loop ...
    
    // 8. Connect
    Task<bool> connectTask = HapticManager.Instance.ConnectCurrentUserAsync();
    yield return new WaitUntil(() => connectTask.IsCompleted);
    
    // 9. Start stimulation
    HapticManager.Instance.ToggleStim();
    
    // 10. Start buzzing
    StartBuzz();
}
```

---

## 📊 Key Differences

| Aspect | Afference SDK | Your Implementation |
|--------|---------------|---------------------|
| **Device ID** | ❌ Not set in UI scripts (must be set elsewhere) | ✅ Explicitly set via `SetDevice()` |
| **Comm Type** | ✅ Auto-set in `Awake()` | ✅ Explicitly set |
| **User Loading** | ✅ Via dropdown/form | ✅ Explicitly set |
| **Permissions** | ✅ Same pattern | ✅ Same pattern |
| **Focus Wait** | ✅ Same pattern | ✅ Same pattern |
| **Connection** | ✅ Same `ConnectCurrentUserAsync()` | ✅ Same method |
| **Session Creation** | ✅ Automatic in `ConnectCurrentUserAsync()` | ✅ Automatic (same) |
| **Stimulation Start** | ✅ Manual (in calibration) | ✅ Automatic after connection |
| **FlexGlove Handling** | ❌ None | ✅ Waits for FlexGlove to finish |
| **Error Handling** | ✅ Retry with exponential backoff | ✅ Same (inherited) |

---

## 🎯 Critical Observations

### **1. Device ID Setting**

**Afference SDK:**
- ❌ `ExistingUserDropdown` does NOT set device ID
- ❌ `NewUserForm` does NOT set device ID
- ⚠️ Device ID must be set elsewhere (possibly in scene setup or another script)

**Your Implementation:**
- ✅ Explicitly sets device ID: `HapticManager.Instance.SetDevice(deviceId)`
- ✅ More explicit and clear

### **2. Connection Flow**

**Both use the same core method:**
```csharp
await HapticManager.Instance.ConnectCurrentUserAsync();
```

**This method:**
1. Validates user, commType, port
2. Creates `AfferenceRingAndroidTransport` (native Android bridge)
3. Wraps in `Transport` class
4. Creates `AfferenceRing` object
5. Calls `CreateSession()` which:
   - Creates `HapticFactory` and `HapticSession`
   - Sets up encoders
   - Loads encoder JSON files
6. Returns success/failure

### **3. Transport Layer**

**Afference SDK uses:**
- `AfferenceRingAndroidTransport` → Native Android bridge
- `AfferenceAndroidBridge` → Java wrapper (`com.afference.afferenceringwrapper.AfferenceRingWrapper`)
- `Transport` → High-level wrapper

**Your FlexGlove uses:**
- `FlexGloveAndroidBridge` → Java wrapper (`com.flexglove.blewrapper.FlexGloveBLEWrapper`)
- Similar pattern, different Java classes

### **4. Session vs Connection**

**Important distinction:**
- **Connection** = BLE transport established (`[Transport] Open OK`)
- **Session** = Haptic system initialized (`CreateSession()`)
- Connection can succeed but session creation can fail (this might be your issue!)

---

## 🔍 What Your Logs Show

Based on your logs:
```
[Transport] Open OK  ✅ Connection successful
```

But missing:
```
[HapticManager] Creating AfferenceRing object...
[HapticManager] AfferenceRing created, calling CreateSession...
[HapticManager] CreateSession completed successfully
```

**This suggests:** Connection succeeds, but `CreateSession()` or `AfferenceRing` creation is failing silently or being interrupted.

---

## 💡 Recommendations

### **1. Add More Logging**

Your implementation already has good logging. The Afference SDK's `ConnectCurrentUserAsync()` has minimal logging between transport open and session creation.

### **2. Check Session Creation**

The issue might be in `CreateSession()`:
- User data might be invalid
- Encoder files might be missing
- BodyLocation data might be incorrect

### **3. Compare Error Handling**

Afference SDK catches exceptions in the retry loop. Your implementation inherits this, but exceptions might be swallowed.

### **4. FlexGlove Conflict**

Your addition of waiting for FlexGlove is good, but ensure:
- FlexGlove is fully disconnected before Afference connects
- BLE stack has time to release resources
- No GATT conflicts

---

## 📝 Summary

**Afference SDK Flow:**
1. `HapticManager.Awake()` → Auto-sets `commType = "ble"`
2. User selection → `LoadUser()` → `EnsureBlePermissionsAsync()` → `WaitForAndroidFocusAndStabilityAsync()` → `ConnectCurrentUserAsync()`
3. `ConnectCurrentUserAsync()` → Creates transport → Creates ring → Creates session
4. Calibration → Activates encoders → Sends test pulses

**Your Implementation:**
- ✅ Follows same pattern
- ✅ More explicit (sets device ID)
- ✅ Adds FlexGlove conflict prevention
- ✅ Auto-starts stimulation and buzzing

**Key Difference:** Afference SDK relies on device ID being set elsewhere, while yours sets it explicitly. Both use the same underlying connection method.

