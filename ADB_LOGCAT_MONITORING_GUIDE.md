# ADB Logcat Monitoring Guide for FlexGlove BLE

## Quick Start

**Recommended Command:**

```bash
adb logcat -v time Unity:V FlexGloveBLE:V FlexGloveBridge:V FlexGloveBLEWrapper:V *:S
```

Or use the batch file:

```bash
.\view_unity_logs.bat
```

---

## 🎯 Key Things to Watch For (Quick Reference)

### ✅ Success Indicators (In Order):

1. `[FlexGloveBLEWrapper] Permissions granted`
2. `[FlexGloveBridge] Init OK`
3. `FlexGloveBLE: Found device: FlexGlove-ESP32`
4. `[FlexGloveBLEWrapper] Found target device: FlexGlove-ESP32`
5. `FlexGloveBLE: Connected to GATT server`
6. `FlexGloveBLE: Services discovered` ← **Critical: No "not found" errors here!**
7. `[FlexGloveBridge] OPEN OK`
8. `[FlexGloveBLEWrapper] Successfully connected`
9. `[FlexGloveBLEWrapper] Starting flex sensor value reader`
10. **Values updating:** Check Unity Inspector → `thumb`, `index`, `middle`, `ring`, `pinky` should be changing
11. **VR Text Fields:** ThumbText, IndexText, etc. should show "Thumb: 1234" format

### 🚨 Critical Errors (Stop and Fix):

- `Service not found` → UUID mismatch
- `Characteristic not found` → UUID mismatch
- `Stopped reading flex sensor values` immediately → Connection state issue
- Values stay at 0 → ESP32 not sending data or format mismatch

---

## Expected Log Flow (Success Case)

### Stage 1: Initialization & Permissions

**Look for:**

```
[FlexGloveBLEWrapper] Requesting Android permissions...
[FlexGloveBLEWrapper] Permissions granted
```

**✅ Success:** "Permissions granted"  
**❌ Failure:** "BLUETOOTH_SCAN permission denied" or "BLUETOOTH_CONNECT permission denied"

---

### Stage 2: Bridge Initialization

**Look for:**

```
[FlexGloveBridge] Init OK
```

**✅ Success:** "Init OK"  
**❌ Failure:**

- "[FlexGloveBridge] Init FAILED: [error details]"
- "[FlexGloveBLEWrapper] Failed to initialize bridge"

---

### Stage 3: Device Scanning

**Look for:**

```
[FlexGloveBLEWrapper] Scanning for devices...
[FlexGloveBridge] Started scanning for 5000ms
FlexGloveBLE: Starting BLE device scan for 5000ms
FlexGloveBLE: Found device: FlexGlove-ESP32 (80:F3:DA:A9:64:9A)
FlexGloveBLE: Found device: [other devices...]
FlexGloveBLE: Scan complete. Found X device(s)
[FlexGloveBLEWrapper] Found X device(s):
  - FlexGlove-ESP32 (80:F3:DA:A9:64:9A)
  - [other devices...]
```

**✅ Success:**

- "Found device: FlexGlove-ESP32"
- "Scan complete. Found X device(s)"
- Your device appears in the list with MAC address
- Device list logged with " - " prefix

**❌ Failure:**

- "[FlexGloveBLEWrapper] Query failed: [error]"
- "Scan failed: [error code]"
- "Scan timeout"
- "[FlexGloveBLEWrapper] No BLE devices found during scan"
- Device list is empty

---

### Stage 4: Device Detection

**Look for:**

```
[FlexGloveBLEWrapper] Found target device: FlexGlove-ESP32 (80:F3:DA:A9:64:9A)
```

**✅ Success:** "Found target device: FlexGlove-ESP32"  
**❌ Failure:** "Target device 'FlexGlove-ESP32' not found in scan results"

---

### Stage 5: Connection Attempt

**Look for:**

```
[FlexGloveBLEWrapper] Attempting to connect to FlexGlove-ESP32...
[FlexGloveBLE] Connecting to FlexGlove-ESP32...
[FlexGloveBridge] openAsync deviceName='FlexGlove-ESP32' (timeoutMs=10000)
FlexGloveBLE: Starting scan for device: FlexGlove-ESP32
FlexGloveBLE: Found device: FlexGlove-ESP32
FlexGloveBLE: Connecting to device...
FlexGloveBLE: Connected to GATT server
FlexGloveBLE: Services discovered
FlexGloveBLE: Characteristic not found  <-- WATCH FOR THIS!
FlexGloveBLE: Service not found  <-- OR THIS!
[FlexGloveBridge] onOpen handle=0x1
[FlexGloveBridge] OPEN OK
[FlexGloveBLE] Connected to FlexGlove-ESP32
[FlexGloveBLEWrapper] Successfully connected to FlexGlove-ESP32
```

**✅ Success:**

- "Connected to GATT server"
- "Services discovered"
- "OPEN OK"
- "Successfully connected"

**❌ Failure Indicators:**

- "Characteristic not found" → **UUID mismatch!**
- "Service not found" → **UUID mismatch!**
- "Connection timeout"
- "Connection failed"
- "Connection to FlexGlove-ESP32 failed or timed out"

---

### Stage 6: Data Reading Started

**Look for:**

```
[FlexGloveBLEWrapper] Device connected! Starting flex sensor data reading coroutine...
[FlexGloveBLEWrapper] Starting flex sensor value reader (interval: 0.1s)
```

**✅ Success:**

- "Device connected! Starting flex sensor data reading coroutine..."
- "Starting flex sensor value reader"
- **Should NOT see:** "Stopped reading flex sensor values" immediately after starting

**❌ Failure Indicators:**

- "Stopped reading flex sensor values" appears right after starting → Connection state issue (should be fixed now)
- No "Starting flex sensor value reader" message → Connection didn't complete

---

### Stage 7: Data Reception (Ongoing)

**Look for:**

```
[FlexGloveBLE] (no specific logs during normal operation, but data should be flowing)
```

**Check in Unity Inspector:**

- `thumb`, `index`, `middle`, `ring`, `pinky` values should be updating every 0.1 seconds
- `connectionStatus` should show "Connected - Reading Flex Sensor Data"
- Values should change when you bend/straighten fingers on the glove

**Expected Value Range:**

- ESP32 12-bit ADC: 0-4095
- ESP32 10-bit ADC: 0-1023
- Lower values = finger bent/curled
- Higher values = finger straight

**Check Text Fields in VR:**

- ThumbText, IndexText, MiddleText, RingText, PinkyText should display:
  - "Thumb: 1234"
  - "Index: 2345"
  - etc.

**❌ No Data Indicators:**

- Values stay at 0
- Connection status shows "Disconnected"
- "Stopped reading flex sensor values" appears
- Check ESP32 serial monitor to verify it's sending data
- Verify ESP32 is sending format: `"Flex: T:123 I:456 M:789 R:234 P:567"` or CSV format

---

## Common Error Messages & Solutions

### 1. Permission Errors

```
[FlexGloveBLEWrapper] BLUETOOTH_SCAN permission denied
```

**Solution:** User needs to grant permissions in Quest settings

### 2. UUID Mismatch

```
FlexGloveBLE: Service not found
FlexGloveBLE: Characteristic not found
```

**Solution:** Check that ESP32 UUIDs match Java wrapper:

- ESP32 Service: `a7f3c9e1-4b2d-8f6a-1c3e-9d5b7a2f4e8c`
- ESP32 Characteristic: `d8e4f2a6-3c1b-7e9d-2a4f-6c8b1e3d5a7f`
- Java wrapper should match (already fixed)

### 3. Device Not Found

```
[FlexGloveBLEWrapper] Target device 'FlexGlove-ESP32' not found in scan results
```

**Solutions:**

- ESP32 not powered on
- ESP32 not advertising
- Device name mismatch (check ESP32 code)
- Too far away / signal too weak
- Scan duration too short

### 4. Connection Timeout

```
[FlexGloveBridge] openAsync timed out waiting for onOpen
```

**Solutions:**

- ESP32 might be connected to another device
- Increase `connectionTimeoutMs`
- Restart ESP32

### 5. Scan Failed

```
FlexGloveBLE: Device scan failed: [error code]
```

**Error Codes:**

- `1` = SCAN_FAILED_APPLICATION_REGISTRATION_FAILED
- `2` = SCAN_FAILED_INTERNAL_ERROR
- `3` = SCAN_FAILED_FEATURE_UNSUPPORTED
- `4` = SCAN_FAILED_OUT_OF_HARDWARE_RESOURCES
- `5` = SCAN_FAILED_SCANNING_TOO_FREQUENTLY

**Solutions:**

- Restart Bluetooth on Quest
- Wait longer between scans
- Check if another app is scanning

---

## Quick Debugging Commands

### View only errors:

```bash
adb logcat -v time Unity:E FlexGloveBLE:E FlexGloveBridge:E FlexGloveBLEWrapper:E *:S
```

### View everything FlexGlove related:

```bash
adb logcat -v time | findstr /i "flexglove"
```

### Clear logs and start fresh:

```bash
adb logcat -c && adb logcat -v time Unity:V FlexGloveBLE:V FlexGloveBridge:V FlexGloveBLEWrapper:V *:S
```

### Save logs to file:

```bash
adb logcat -v time Unity:V FlexGloveBLE:V FlexGloveBridge:V FlexGloveBLEWrapper:V *:S > flexglove_logs.txt
```

---

## Expected Timeline

1. **0-2 seconds:** Permissions & initialization
2. **2-7 seconds:** Device scanning (5 second scan, configurable via `scanDurationMs`)
3. **7-8 seconds:** Device detection
4. **8-18 seconds:** Connection attempt (up to 10 second timeout)
5. **18+ seconds:** Data reading (ongoing, updates every 0.1s by default)

**Total time to connection:** ~8-18 seconds

**After Connection:**

- Flex sensor values should start updating immediately
- Text fields in VR should display values within 0.1-0.2 seconds
- Values update continuously at `readIntervalSeconds` interval (default: 0.1s)

---

## Post-Connection: What to Verify

After successful connection, verify these in order:

### 1. Connection State (Should see all of these):

```
[FlexGloveBLEWrapper] Successfully connected to FlexGlove-ESP32
[FlexGloveBLEWrapper] Device connected! Starting flex sensor data reading coroutine...
[FlexGloveBLEWrapper] Starting flex sensor value reader (interval: 0.1s)
```

### 2. Coroutine Should Keep Running:

- **Should NOT see:** "Stopped reading flex sensor values" immediately
- If you see "Stopped" right away → Connection state issue (check `IsConnected` and `bleReader.IsConnected`)

### 3. Data Should Be Flowing:

- Check Unity Inspector → `FlexGloveBLEWrapper` component
- `thumb`, `index`, `middle`, `ring`, `pinky` should be non-zero and changing
- `connectionStatus` should show "Connected - Reading Flex Sensor Data"

### 4. VR Text Fields Should Update:

- ThumbText, IndexText, MiddleText, RingText, PinkyText should display values
- Format: "Thumb: 1234", "Index: 2345", etc.
- Values should change when you move fingers on the glove

### 5. If Values Stay at 0:

- ESP32 might not be sending data
- Check ESP32 serial monitor to verify it's sending
- Verify data format matches what Unity expects
- Check that ESP32 is actually connected (not just Quest connecting to it)

---

## Red Flags to Watch For

🚨 **STOP HERE IF YOU SEE:**

- "Permission denied" → Fix permissions first
- "Init FAILED" → Check Unity build settings
- "Service not found" → UUID mismatch, check ESP32 code
- "Characteristic not found" → UUID mismatch, check ESP32 code
- "Connection timeout" → ESP32 might not be advertising or already connected
- "Stopped reading flex sensor values" immediately after connection → Connection state issue

✅ **GOOD SIGNS:**

- "Permissions granted"
- "Init OK"
- "Found device: FlexGlove-ESP32"
- "Found target device: FlexGlove-ESP32"
- "Connected to GATT server"
- "Services discovered"
- "OPEN OK"
- "Successfully connected to FlexGlove-ESP32"
- "Device connected! Starting flex sensor data reading coroutine..."
- "Starting flex sensor value reader"
- Flex sensor values updating in Unity Inspector (thumb, index, middle, ring, pinky)
- Text fields in VR showing values (ThumbText, IndexText, etc.)

---

## Testing Checklist

- [ ] Permissions granted
- [ ] Bridge initialized
- [ ] Device scan finds FlexGlove-ESP32
- [ ] Device detected in results (with MAC address)
- [ ] Connection succeeds
- [ ] Services discovered (no "not found" errors)
- [ ] Data reading coroutine starts (no immediate "Stopped" message)
- [ ] Flex sensor values updating in Unity Inspector (thumb, index, middle, ring, pinky)
- [ ] Text fields in VR displaying values (ThumbText, IndexText, MiddleText, RingText, PinkyText)
- [ ] Values change when bending/straightening fingers on glove

---

## What to Look For: Flex Sensor Values

### What Are We Reading?

The system reads **flex sensor analog values** from your ESP32 glove:

- **`thumb`** - Flex sensor reading for thumb (0-4095 for ESP32 12-bit ADC)
- **`index`** - Flex sensor reading for index finger
- **`middle`** - Flex sensor reading for middle finger
- **`ring`** - Flex sensor reading for ring finger
- **`pinky`** - Flex sensor reading for pinky finger

### Value Interpretation:

- **Lower values** (e.g., 0-1000) = Finger **bent/curled** (more resistance)
- **Higher values** (e.g., 3000-4095) = Finger **straight** (less resistance)

### Data Format from ESP32:

ESP32 sends: `"Flex: T:1234 I:2345 M:3456 R:1234 P:2345"`

Unity parser handles:

- ESP32 format: `"Flex: T:123 I:456 M:789 R:234 P:567"`
- CSV format: `"thumb,index,middle,ring,pinky\n"`

### Where Values Appear:

1. **Unity Inspector:**

   - `FlexGloveBLEWrapper` component → `thumb`, `index`, `middle`, `ring`, `pinky` fields
   - Updates every 0.1 seconds (configurable via `readIntervalSeconds`)

2. **VR Text Fields:**
   - ThumbText: "Thumb: 1234"
   - IndexText: "Index: 2345"
   - MiddleText: "Middle: 3456"
   - RingText: "Ring: 1234"
   - PinkyText: "Pinky: 2345"

### Debugging Data Flow:

To see values in logcat, temporarily enable debug logging in `FlexGloveBluetoothReader.cs`:

- Uncomment line 219: `Debug.Log($"Glove BLE: Thumb:{thumb} Index:{index} Middle:{middle} Ring:{ring} Pinky:{pinky}");`

This will show values as they're parsed from BLE data.
