# Afference Ring Pairing & Testing Guide

## Quick Start - Testing Your Script

### Setup Steps:

1. **Add the script to your scene:**
   - Create an empty GameObject (or use existing one)
   - Add `AfferenceRingPairing` component to it
   - Verify settings in Inspector:
     - `Device Id`: "6f28" (should be default)
     - `User Name`: "ExampleUser" (should be default)
     - `Auto Start On Enable`: ✓ (checked)

2. **Ensure HapticManager exists:**
   - Your scene should have a GameObject with `HapticManager` component
   - HapticManager should be set up as a singleton (Instance pattern)

3. **Build and deploy to Quest/Android device**

4. **Start logcat monitoring** (see commands below)

5. **Run the scene** - The script will automatically start pairing when enabled

---

## Logcat Commands

### Recommended Command (All Unity logs):
```bash
adb logcat -v time Unity:V *:S
```

### Filter for Afference/Haptic related logs:
```bash
adb logcat -v time Unity:V | findstr /i "Afference HapticManager Ring pairing buzz"
```

### View only errors:
```bash
adb logcat -v time Unity:E *:S
```

### Clear logs and start fresh:
```bash
adb logcat -c && adb logcat -v time Unity:V *:S
```

### Save logs to file:
```bash
adb logcat -v time Unity:V *:S > afference_ring_logs.txt
```

---

## 🎯 What to Look For in Logcat (Success Flow)

### Stage 1: Script Initialization ✅

**Look for:**
```
Starting Afference Ring pairing...
Device set to: 6f28
Communication type set to BLE
User loaded: ExampleUser
```

**✅ Success:** All three messages appear  
**❌ Failure:** 
- "HapticManager.Instance is null!" → HapticManager not in scene
- "User loaded" fails → ExampleUser.json not found (check StreamingAssets/Users/)

---

### Stage 2: BLE Permissions (Android Only) ✅

**Look for:**
```
Requesting BLE permissions...
BLE permissions granted
```

**✅ Success:** "BLE permissions granted"  
**❌ Failure:** 
- "BLE permissions denied!" → User needs to grant permissions in Quest settings
- Permission dialog appears → Grant BLUETOOTH_SCAN and BLUETOOTH_CONNECT

**Note:** On first run, Android will show a permission dialog. User must grant permissions.

---

### Stage 3: Connection Attempt ✅

**Look for:**
```
Connecting to ring...
[HapticManager] Attempting connection...
[Transport] Open 'Afference SBRing-6f28' (timeoutMs=12000)
```

**✅ Success:** Connection proceeds without errors  
**❌ Failure:** 
- "No user loaded." → User file not loaded properly
- "commType not set" → SetCommType not called
- "port/device not set" → SetDevice not called

---

### Stage 4: Transport Connection ✅

**Look for:**
```
[Transport] Open 'Afference SBRing-6f28' (timeoutMs=12000)
[Transport] Open OK
```

**✅ Success:** "Open OK" appears  
**❌ Failure:**
- "Open failed or timed out" → Ring not found or not advertising
- "Open exception" → BLE connection issue
- Connection retries → Ring might be connected to another device

**Note:** The script will retry up to 5 times with exponential backoff.

---

### Stage 5: Session Creation ✅

**Look for:**
```
[HapticManager] Connected!
Successfully connected to Afference Ring!
Stimulation started
```

**✅ Success:** "Successfully connected" and "Stimulation started"  
**❌ Failure:**
- "Could not connect to the Afference Ring" → Check ring is powered on and advertising
- Connection retries fail → See error details in logs

---

### Stage 6: Periodic Buzzing ✅

**Look for:**
```
Started periodic buzz (interval: 1s, intensity: 0.5)
```

**✅ Success:** Buzz message appears  
**Physical verification:** You should feel periodic vibrations on your finger

**Note:** The script sends haptic values every `buzzInterval` seconds. Each buzz lasts `buzzDuration` seconds.

---

## 🚨 Common Errors & Solutions

### Error 1: "HapticManager.Instance is null!"

**Solution:**
- Ensure HapticManager GameObject exists in scene
- HapticManager must have `DontDestroyOnLoad` and singleton pattern
- Check that HapticManager's `Awake()` runs before AfferenceRingPairing's `OnEnable()`

---

### Error 2: "No user loaded" or User file not found

**Solution:**
- Verify `ExampleUser.json` exists in `StreamingAssets/Users/`
- Check that `CopyAssets.CopyAllAssets()` runs (called in HapticManager.Awake)
- User file should be copied to `Application.persistentDataPath/Users/ExampleUser.json`

---

### Error 3: "BLE permissions denied"

**Solution:**
- On Quest: Settings → Apps → Your App → Permissions → Grant Bluetooth permissions
- Or restart app and grant when dialog appears
- For Android 12+: Need BLUETOOTH_SCAN and BLUETOOTH_CONNECT

---

### Error 4: "Could not connect to the Afference Ring"

**Possible causes:**
- Ring not powered on
- Ring not advertising (check ring's LED/status)
- Ring already connected to another device
- Device ID mismatch (verify ring's actual ID is "6f28")
- Ring too far away / weak signal
- Wrong device name format (should be "Afference SBRing-6f28")

**Solutions:**
- Power cycle the ring
- Check ring is in pairing/advertising mode
- Disconnect ring from other devices
- Verify device ID matches your ring
- Move ring closer to Quest
- Check `HapticManager.Instance.status` for detailed error message

---

### Error 5: Connection retries fail

**Look for:**
```
[HapticManager] Attempt 1/5 failed: [error details]
[HapticManager] Attempt 2/5 failed: [error details]
...
```

**Solutions:**
- Check the specific exception in logs
- Verify ring is advertising (check ring's status LED)
- Try increasing `maxAttempts` in `ConnectCurrentUserAsync()`
- Check Android Bluetooth is enabled
- Restart Quest Bluetooth

---

### Error 6: No buzz felt / "Ring not connected"

**Check:**
- `isConnected` flag is true
- `HapticManager.Instance.stimActive` is true
- Buzz coroutine is running (check Unity Inspector)
- Haptic intensity might be too low (try increasing `buzzIntensity` to 1.0)

---

## Expected Timeline

1. **0-1 seconds:** Script initialization, device/user setup
2. **1-3 seconds:** BLE permissions (if needed, includes dialog wait)
3. **3-15 seconds:** Connection attempt (up to 12 second timeout per attempt, max 5 attempts)
4. **15+ seconds:** Periodic buzzing starts

**Total time to first buzz:** ~15-20 seconds (if all goes well)

---

## Testing Checklist

- [ ] HapticManager exists in scene
- [ ] AfferenceRingPairing script added to GameObject
- [ ] Device ID set to "6f28" (or your ring's ID)
- [ ] ExampleUser.json exists in StreamingAssets/Users/
- [ ] Quest/Android device connected via ADB
- [ ] Logcat monitoring started
- [ ] Ring is powered on and advertising
- [ ] Bluetooth enabled on Quest
- [ ] Permissions granted (if prompted)
- [ ] Connection succeeds (see "Successfully connected")
- [ ] Stimulation starts (see "Stimulation started")
- [ ] Periodic buzz starts (see "Started periodic buzz")
- [ ] **Physical test:** Feel vibrations on finger

---

## Debugging Tips

### 1. Check HapticManager Status

In Unity, you can check `HapticManager.Instance.status` to see current connection status:
- "Attempting connection..."
- "Retrying... (2/5)"
- "Connected!"
- "Could not connect to the Afference Ring.\nPlease restart the app."

### 2. Verify Ring is Advertising

- Check ring's LED status
- Use a BLE scanner app to verify ring is visible
- Device name should match: "Afference SBRing-6f28"

### 3. Test Manual Buzz

Call `AfferenceRingPairing.TriggerBuzz()` to test a single buzz without waiting for periodic timing.

### 4. Adjust Buzz Settings

In Inspector, you can adjust:
- `Buzz Interval`: Time between buzzes (default: 1.0s)
- `Buzz Intensity`: Haptic strength 0-1 (default: 0.5)
- `Buzz Duration`: How long each buzz lasts (default: 0.1s)

### 5. Monitor Connection State

Watch `HapticManager.Instance.stimActive` - should be `true` after successful connection.

---

## Success Indicators Summary

✅ **All of these should appear in order:**

1. "Starting Afference Ring pairing..."
2. "Device set to: 6f28"
3. "Communication type set to BLE"
4. "User loaded: ExampleUser"
5. "Requesting BLE permissions..." (Android only)
6. "BLE permissions granted" (Android only)
7. "Connecting to ring..."
8. "[Transport] Open 'Afference SBRing-6f28'"
9. "[Transport] Open OK"
10. "Successfully connected to Afference Ring!"
11. "Stimulation started"
12. "Started periodic buzz (interval: 1s, intensity: 0.5)"
13. **Physical:** Feel periodic vibrations on finger

---

## Quick Reference: Logcat Filter Commands

**Windows PowerShell:**
```powershell
adb logcat -v time Unity:V *:S | Select-String -Pattern "Afference|Haptic|Ring|pairing|buzz|Transport"
```

**Windows CMD:**
```cmd
adb logcat -v time Unity:V *:S | findstr /i "Afference Haptic Ring pairing buzz Transport"
```

**Linux/Mac:**
```bash
adb logcat -v time Unity:V *:S | grep -i "Afference\|Haptic\|Ring\|pairing\|buzz\|Transport"
```

