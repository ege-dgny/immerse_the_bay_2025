# BLE Concurrent Connection Fix

## Problem
The FlexGlove and Afference Ring BLE implementations were using **static GATT references**, which prevented concurrent connections. When one device connected, it would close the other device's connection.

## Solution Implemented

### 1. FlexGloveBLEWrapper.java
- **Changed**: `private static BluetoothGatt gatt;` → `private BluetoothGatt gatt;`
- **Result**: Each `FlexGloveBLEWrapper` instance now maintains its own GATT connection
- **Updated**: `hardCloseGatt()` is now instance-based (non-static)

### 2. FlexGloveAndroidBridge.cs
- **Changed**: `HardCloseGatt()` from static to instance method
- **Result**: Only closes the GATT connection for that specific bridge instance
- **Impact**: Multiple FlexGlove bridges can coexist without interfering

### 3. AfferenceAndroidBridge.cs
- **Removed**: `HardCloseGatt()` call before `OpenBlocking()` (line 50)
- **Reason**: This was closing FlexGlove's connection when Afference ring tried to connect
- **Kept**: `HardCloseGatt()` in `CloseBlocking()` for cleanup (only affects Afference ring's static GATT)

### 4. AfferenceRingPairing.cs
- **Updated**: Comments to reflect that concurrent connections are now supported
- **Note**: The wait for FlexGlove is now mainly for BLE stack stability, not conflict prevention

## Current Status

✅ **FlexGlove**: Fully supports concurrent connections (instance-based GATT)

⚠️ **Afference Ring**: Still uses static GATT in `AfferenceRingWrapper` (Java AAR)
- The AAR file contains compiled Java code that we cannot modify
- `AfferenceAndroidBridge.HardCloseGatt()` only affects Afference ring's static GATT
- This should not interfere with FlexGlove's instance-based connections

## Testing

To test concurrent connections:

1. **Start FlexGlove connection first**
   - FlexGlove should connect successfully
   - Verify data is being received

2. **Then start Afference Ring connection**
   - Afference ring should connect without disconnecting FlexGlove
   - Both devices should work simultaneously

3. **Monitor logs** for:
   - `[FlexGloveBridge] HardCloseGatt: closed this instance's GATT.` (instance-based)
   - `[Bridge] HardCloseGatt: closing Afference ring GATT only` (static, but isolated)

## Future Improvement

If you have access to the `AfferenceRingWrapper` Java source code, update it to use instance-based GATT:
- Change `static BluetoothGatt gatt;` to `private BluetoothGatt gatt;`
- Make `hardCloseGatt()` instance-based instead of static
- This will provide true concurrent connection support for both devices

## Android BLE GATT Support

Android's BLE stack **does support multiple concurrent GATT connections**. The limitation was in the implementation (static references), not the platform. With instance-based GATT connections, you can connect to multiple BLE devices simultaneously.

