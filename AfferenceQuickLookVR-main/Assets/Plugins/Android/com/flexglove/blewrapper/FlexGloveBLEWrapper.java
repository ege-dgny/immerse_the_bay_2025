package com.flexglove.blewrapper;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattDescriptor;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothProfile;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanFilter;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

public class FlexGloveBLEWrapper {
    private static final String TAG = "FlexGloveBLE";
    private static BluetoothGatt gatt;
    
    // UUIDs matching ESP32 code (must match ESP32_FlexGlove_BLE_Debug.ino)
    private static final String SERVICE_UUID_STR = "a7f3c9e1-4b2d-8f6a-1c3e-9d5b7a2f4e8c";
    private static final String CHARACTERISTIC_UUID_STR = "d8e4f2a6-3c1b-7e9d-2a4f-6c8b1e3d5a7f";
    
    private static final UUID SERVICE_UUID = UUID.fromString(SERVICE_UUID_STR);
    private static final UUID CHARACTERISTIC_UUID = UUID.fromString(CHARACTERISTIC_UUID_STR);
    private static final UUID CLIENT_CHARACTERISTIC_CONFIG = UUID.fromString("00002902-0000-1000-8000-00805f9b34fb");
    
    private Context context;
    private BluetoothAdapter bluetoothAdapter;
    private BluetoothLeScanner scanner;
    private Handler handler;
    private OpenListener openListener;
    private RxListener rxListener;
    private String deviceName;
    private BluetoothDevice targetDevice;
    private BluetoothGattCharacteristic dataCharacteristic;
    private byte[] rxBuffer = new byte[0];
    private boolean isConnected = false;
    private ScanResultListener scanResultListener;
    private ArrayList<String> scannedDevices = new ArrayList<>();
    private boolean isScanning = false;
    
    public interface OpenListener {
        void onOpen(long handle);
        void onError(String error);
    }
    
    public interface RxListener {
        void onRx(byte[] data);
    }
    
    public interface ScanResultListener {
        void onScanResult(String[] deviceNames);
        void onScanError(String error);
    }
    
    public FlexGloveBLEWrapper(Context ctx) {
        this.context = ctx;
        this.handler = new Handler(Looper.getMainLooper());
        
        BluetoothManager bluetoothManager = (BluetoothManager) ctx.getSystemService(Context.BLUETOOTH_SERVICE);
        if (bluetoothManager != null) {
            bluetoothAdapter = bluetoothManager.getAdapter();
            if (bluetoothAdapter != null) {
                scanner = bluetoothAdapter.getBluetoothLeScanner();
            }
        }
    }
    
    public void openAsync(String deviceName, int timeoutMs, OpenListener listener) {
        this.deviceName = deviceName;
        this.openListener = listener;
        
        if (bluetoothAdapter == null || !bluetoothAdapter.isEnabled()) {
            if (listener != null) {
                listener.onError("Bluetooth not enabled");
            }
            return;
        }
        
        if (scanner == null) {
            if (listener != null) {
                listener.onError("BLE scanner not available");
            }
            return;
        }
        
        Log.d(TAG, "Starting scan for device: " + deviceName);
        
        // Create scan filter
        ScanFilter.Builder filterBuilder = new ScanFilter.Builder();
        filterBuilder.setDeviceName(deviceName);
        List<ScanFilter> filters = new ArrayList<>();
        filters.add(filterBuilder.build());
        
        ScanSettings settings = new ScanSettings.Builder()
            .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
            .build();
        
        // Start scanning
        scanner.startScan(filters, settings, scanCallback);
        
        // Timeout handler
        handler.postDelayed(() -> {
            if (!isConnected && scanner != null) {
                scanner.stopScan(scanCallback);
                if (openListener != null) {
                    openListener.onError("Connection timeout");
                }
            }
        }, timeoutMs > 0 ? timeoutMs : 10000);
    }
    
    private ScanCallback scanCallback = new ScanCallback() {
        @Override
        public void onScanResult(int callbackType, ScanResult result) {
            BluetoothDevice device = result.getDevice();
            String name = device.getName();
            
            if (name != null && name.equals(deviceName)) {
                Log.d(TAG, "Found device: " + name);
                scanner.stopScan(scanCallback);
                targetDevice = device;
                connectToDevice();
            }
        }
        
        @Override
        public void onScanFailed(int errorCode) {
            Log.e(TAG, "Scan failed: " + errorCode);
            if (openListener != null) {
                openListener.onError("Scan failed: " + errorCode);
            }
        }
    };
    
    private void connectToDevice() {
        if (targetDevice == null) return;
        
        Log.d(TAG, "Connecting to device...");
        gatt = targetDevice.connectGatt(context, false, gattCallback);
    }
    
    private BluetoothGattCallback gattCallback = new BluetoothGattCallback() {
        @Override
        public void onConnectionStateChange(BluetoothGatt gatt, int status, int newState) {
            if (newState == BluetoothProfile.STATE_CONNECTED) {
                Log.d(TAG, "Connected to GATT server");
                isConnected = true;
                gatt.discoverServices();
            } else if (newState == BluetoothProfile.STATE_DISCONNECTED) {
                Log.d(TAG, "Disconnected from GATT server");
                isConnected = false;
            }
        }
        
        @Override
        public void onServicesDiscovered(BluetoothGatt gatt, int status) {
            if (status == BluetoothGatt.GATT_SUCCESS) {
                Log.d(TAG, "Services discovered");
                BluetoothGattService service = gatt.getService(SERVICE_UUID);
                if (service != null) {
                    dataCharacteristic = service.getCharacteristic(CHARACTERISTIC_UUID);
                    if (dataCharacteristic != null) {
                        // Enable notifications
                        Log.d(TAG, "Enabling notifications for characteristic: " + CHARACTERISTIC_UUID);
                        boolean notifyResult = gatt.setCharacteristicNotification(dataCharacteristic, true);
                        Log.d(TAG, "setCharacteristicNotification result: " + notifyResult);
                        
                        BluetoothGattDescriptor descriptor = dataCharacteristic.getDescriptor(CLIENT_CHARACTERISTIC_CONFIG);
                        if (descriptor != null) {
                            descriptor.setValue(BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE);
                            boolean writeResult = gatt.writeDescriptor(descriptor);
                            Log.d(TAG, "writeDescriptor (enable notifications) result: " + writeResult);
                        } else {
                            Log.e(TAG, "CLIENT_CHARACTERISTIC_CONFIG descriptor not found!");
                        }
                        
                        // Notify Unity that connection is ready
                        if (openListener != null) {
                            handler.post(() -> openListener.onOpen(1));
                        }
                    } else {
                        Log.e(TAG, "Characteristic not found");
                        if (openListener != null) {
                            openListener.onError("Characteristic not found");
                        }
                    }
                } else {
                    Log.e(TAG, "Service not found");
                    if (openListener != null) {
                        openListener.onError("Service not found");
                    }
                }
            }
        }
        
        @Override
        public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) {
            if (characteristic.getUuid().equals(CHARACTERISTIC_UUID)) {
                byte[] data = characteristic.getValue();
                if (data != null && data.length > 0) {
                    String text = new String(data);
                    Log.d(TAG, "onCharacteristicChanged: received " + data.length + " bytes: " + text);
                    if (rxListener != null) {
                        rxListener.onRx(data);
                    } else {
                        Log.w(TAG, "onCharacteristicChanged: rxListener is null!");
                    }
                } else {
                    Log.w(TAG, "onCharacteristicChanged: data is null or empty");
                }
            } else {
                Log.w(TAG, "onCharacteristicChanged: UUID mismatch. Got: " + characteristic.getUuid() + ", expected: " + CHARACTERISTIC_UUID);
            }
        }
    };
    
    public void setRxListener(RxListener listener) {
        this.rxListener = listener;
    }
    
    public void close(long handle) {
        if (gatt != null) {
            try {
                gatt.disconnect();
                gatt.close();
            } catch (Exception e) {
                Log.e(TAG, "Error closing GATT: " + e.getMessage());
            }
            gatt = null;
        }
        isConnected = false;
        if (scanner != null) {
            scanner.stopScan(scanCallback);
        }
    }
    
    public static void hardCloseGatt() {
        if (gatt != null) {
            try {
                gatt.disconnect();
                gatt.close();
            } catch (Exception e) {
                Log.e(TAG, "Error in hardCloseGatt: " + e.getMessage());
            }
            gatt = null;
        }
    }
    
    public boolean isOpen() {
        return isConnected && gatt != null;
    }
    
    public byte[] unityRx(long handle, int timeoutMs) {
        // Return buffered data if available
        if (rxBuffer.length > 0) {
            byte[] result = rxBuffer;
            rxBuffer = new byte[0];
            return result;
        }
        return new byte[0];
    }
    
    /**
     * Scan for BLE devices and return list of device names
     * @param scanDurationMs How long to scan in milliseconds
     * @param listener Callback for scan results
     */
    public void scanForDevices(int scanDurationMs, ScanResultListener listener) {
        this.scanResultListener = listener;
        scannedDevices.clear();
        
        if (bluetoothAdapter == null || !bluetoothAdapter.isEnabled()) {
            if (listener != null) {
                listener.onScanError("Bluetooth not enabled");
            }
            return;
        }
        
        if (scanner == null) {
            if (listener != null) {
                listener.onScanError("BLE scanner not available");
            }
            return;
        }
        
        if (isScanning) {
            Log.w(TAG, "Scan already in progress");
            return;
        }
        
        isScanning = true;
        Log.d(TAG, "Starting BLE device scan for " + scanDurationMs + "ms");
        
        // Scan without filters to find all devices
        ScanSettings settings = new ScanSettings.Builder()
            .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
            .build();
        
        // Start scanning with device discovery callback
        scanner.startScan(null, settings, deviceScanCallback);
        
        // Stop scan after duration
        handler.postDelayed(() -> {
            if (isScanning && scanner != null) {
                scanner.stopScan(deviceScanCallback);
                isScanning = false;
                
                // Return results
                if (scanResultListener != null) {
                    String[] deviceArray = scannedDevices.toArray(new String[0]);
                    Log.d(TAG, "Scan complete. Found " + deviceArray.length + " devices");
                    handler.post(() -> scanResultListener.onScanResult(deviceArray));
                }
            }
        }, scanDurationMs);
    }
    
    private ScanCallback deviceScanCallback = new ScanCallback() {
        @Override
        public void onScanResult(int callbackType, ScanResult result) {
            BluetoothDevice device = result.getDevice();
            String name = device.getName();
            String address = device.getAddress();
            
            // Add device by name only (MAC address not needed for connection)
            // Only include devices that have a name (unnamed devices are usually not useful)
            String deviceInfo = null;
            if (name != null && !name.isEmpty()) {
                deviceInfo = name; // Just the device name, no MAC address
            }
            // Skip devices without names
            
            if (deviceInfo == null) {
                return; // Skip unnamed devices
            }
            
            // Avoid duplicates
            if (!scannedDevices.contains(deviceInfo)) {
                scannedDevices.add(deviceInfo);
                Log.d(TAG, "Found device: " + deviceInfo);
            }
        }
        
        @Override
        public void onScanFailed(int errorCode) {
            Log.e(TAG, "Device scan failed: " + errorCode);
            isScanning = false;
            if (scanResultListener != null) {
                handler.post(() -> scanResultListener.onScanError("Scan failed: " + errorCode));
            }
        }
    };
}

