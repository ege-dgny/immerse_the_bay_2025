using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Main wrapper for FlexGlove BLE communication
/// Handles device discovery, connection, and data reading
/// </summary>
public class FlexGloveBLEWrapper : MonoBehaviour
{
    [Header("Device Settings")]
    [Tooltip("BLE Device name to search for (must match ESP32 device name)")]
    public string bleDeviceName = "FlexGlove-ESP32";

    [Tooltip("BLE Service UUID (must match ESP32 code)")]
    public string serviceUUID = "12345678-1234-1234-1234-123456789abc";

    [Tooltip("BLE Characteristic UUID (must match ESP32 code)")]
    public string characteristicUUID = "12345678-1234-1234-1234-123456789def";

    [Header("Scan Settings")]
    [Tooltip("Scan duration in milliseconds")]
    public int scanDurationMs = 5000;

    [Header("Data Reading Settings")]
    [Tooltip("Interval between reading flex sensor values (in seconds)")]
    public float readIntervalSeconds = 0.1f;

    [Header("Status")]
    [Tooltip("List of scanned BLE devices")]
    public string[] scannedDevices = new string[0];

    [Tooltip("Current connection status")]
    public string connectionStatus = "Not Started";

    [Header("Flex Sensor Values")]
    [Tooltip("Current flex sensor readings (0-1023 or similar, depending on ESP32 ADC resolution)")]
    public int thumb;
    public int index;
    public int middle;
    public int ring;
    public int pinky;

    [Header("Temperature")]
    [Tooltip("Current temperature reading (raw value, not normalized)")]
    public int temperature;

    public bool IsConnected { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
    private FlexGloveAndroidBridge bridge;
    private FlexGloveBluetoothReader bleReader;
    private Coroutine readDataCoroutine;
    private bool isInitialized = false;
#else
    private FlexGloveSerialReader serialReader;
    [Header("Serial Settings (PC/Editor only)")]
    public string serialPortName = "COM5";
#endif

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(InitializeBLE());
#else
        InitializeSerial();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Main initialization coroutine - follows the high-level flow
    /// </summary>
    private IEnumerator InitializeBLE()
    {
        connectionStatus = "Initializing...";
        
        // Step 1: Setup Android Permissions
        bool bCouldSetup = false;
        yield return StartCoroutine(SetupAndroidPermissions((result) => bCouldSetup = result));
        if (!bCouldSetup)
        {
            connectionStatus = "Permission setup failed";
            Debug.LogError("[FlexGloveBLEWrapper] Failed to setup Android permissions");
            yield break;
        }

        // Initialize bridge
        bridge = new FlexGloveAndroidBridge();
        if (!bridge.Init())
        {
            connectionStatus = "Bridge initialization failed";
            Debug.LogError("[FlexGloveBLEWrapper] Failed to initialize bridge");
            yield break;
        }

        // Step 2: Query Available Bluetooth Devices (async)
        connectionStatus = "Scanning for devices...";
        bool bCouldQuery = false;
        string[] deviceArray = new string[0];
        
        yield return StartCoroutine(QueryAvailableBluetoothDevices((devices) =>
        {
            deviceArray = devices;
            bCouldQuery = true;
        }, (error) =>
        {
            Debug.LogError($"[FlexGloveBLEWrapper] Query failed: {error}");
            bCouldQuery = false;
        }));

        if (!bCouldQuery || deviceArray.Length == 0)
        {
            connectionStatus = "No devices found";
            Debug.LogWarning("[FlexGloveBLEWrapper] No BLE devices found during scan");
            yield break;
        }

        scannedDevices = deviceArray;
        Debug.Log($"[FlexGloveBLEWrapper] Found {deviceArray.Length} device(s):");
        foreach (var device in deviceArray)
        {
            Debug.Log($"  - {device}");
        }

        // Step 3: Check if our device is in the results
        string targetDevice = FindDeviceInResults(deviceArray, bleDeviceName);
        if (string.IsNullOrEmpty(targetDevice))
        {
            connectionStatus = $"Device '{bleDeviceName}' not found";
            Debug.LogWarning($"[FlexGloveBLEWrapper] Target device '{bleDeviceName}' not found in scan results");
            yield break;
        }

        Debug.Log($"[FlexGloveBLEWrapper] Found target device: {targetDevice}");

        // Step 4: Connect to the device
        connectionStatus = "Connecting...";
        bool connected = false;
        yield return StartCoroutine(ConnectToDevice(bleDeviceName, (result) => connected = result));
        
        if (!connected)
        {
            connectionStatus = "Connection failed";
            Debug.LogError($"[FlexGloveBLEWrapper] Failed to connect to {bleDeviceName}");
            yield break;
        }

        // Step 5: On successful connection, start reading data
        connectionStatus = "Connected";
        isInitialized = true;
        IsConnected = true; // Set connection status before starting data reading
        OnDeviceConnected();
    }

    /// <summary>
    /// Step 1: Setup Android Permissions
    /// </summary>
    private IEnumerator SetupAndroidPermissions(Action<bool> onComplete)
    {
        Debug.Log("[FlexGloveBLEWrapper] Requesting Android permissions...");

        // Request BLUETOOTH_SCAN permission
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
        {
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
            yield return new WaitForSeconds(1f);
            
            if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
            {
                Debug.LogError("[FlexGloveBLEWrapper] BLUETOOTH_SCAN permission denied");
                onComplete?.Invoke(false);
                yield break;
            }
        }

        // Request BLUETOOTH_CONNECT permission
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
        {
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
            yield return new WaitForSeconds(1f);
            
            if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            {
                Debug.LogError("[FlexGloveBLEWrapper] BLUETOOTH_CONNECT permission denied");
                onComplete?.Invoke(false);
                yield break;
            }
        }

        yield return new WaitForSeconds(0.5f);
        Debug.Log("[FlexGloveBLEWrapper] Permissions granted");
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// Step 2: Query Available Bluetooth Devices (async coroutine)
    /// </summary>
    private IEnumerator QueryAvailableBluetoothDevices(Action<string[]> onSuccess, Action<string> onError)
    {
        bool scanComplete = false;
        bool scanSuccess = false;
        string[] resultDevices = new string[0];
        string errorMessage = "";

        bridge.ScanForDevices(
            scanDurationMs,
            onResult: (devices) =>
            {
                resultDevices = devices;
                scanSuccess = true;
                scanComplete = true;
            },
            onError: (error) =>
            {
                errorMessage = error;
                scanSuccess = false;
                scanComplete = true;
            }
        );

        // Wait for scan to complete
        float elapsed = 0f;
        while (!scanComplete && elapsed < (scanDurationMs / 1000f) + 2f) // Add 2 second buffer
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (scanComplete)
        {
            if (scanSuccess)
            {
                onSuccess?.Invoke(resultDevices);
            }
            else
            {
                onError?.Invoke(errorMessage);
            }
        }
        else
        {
            onError?.Invoke("Scan timeout");
        }
    }

    /// <summary>
    /// Helper: Find device in scan results by name
    /// </summary>
    private string FindDeviceInResults(string[] devices, string targetName)
    {
        foreach (var device in devices)
        {
            // Device string is just the name (e.g., "FlexGlove-ESP32")
            if (device == targetName || device.Contains(targetName))
            {
                return device;
            }
        }
        return null;
    }

    /// <summary>
    /// Step 4: Connect to Device
    /// </summary>
    private IEnumerator ConnectToDevice(string deviceName, Action<bool> onComplete)
    {
        Debug.Log($"[FlexGloveBLEWrapper] Attempting to connect to {deviceName}...");

        // Create reader component for connection management
        // Set properties BEFORE adding component so Start() sees them
        bleReader = gameObject.AddComponent<FlexGloveBluetoothReader>();
        bleReader.deviceName = deviceName;
        bleReader.connectionTimeoutMs = 10000;
        bleReader.autoConnect = false; // We handle connection manually
        
        // Wait for component's Start() to complete and bridge to be created
        // Unity calls Start() on the next frame, so wait 2 frames to be safe
        yield return null;
        yield return null;
        
        // Verify bridge was created
        if (bleReader == null)
        {
            Debug.LogError("[FlexGloveBLEWrapper] Failed to create BLE reader component");
            onComplete?.Invoke(false);
            yield break;
        }

        // Connect using the reader
        bool connected = false;
        bool connectionComplete = false;

        bleReader.Connect();
        
        // Wait for connection
        float timeout = 12f; // 10s timeout + 2s buffer
        float elapsed = 0f;
        
        while (!connectionComplete && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
            
            if (bleReader.IsConnected)
            {
                connected = true;
                connectionComplete = true;
            }
            else if (bleReader.ConnectionStatus.Contains("failed") || 
                     bleReader.ConnectionStatus.Contains("Failed"))
            {
                connected = false;
                connectionComplete = true;
            }
        }

        if (connected)
        {
            Debug.Log($"[FlexGloveBLEWrapper] Successfully connected to {deviceName}");
        }
        else
        {
            Debug.LogError($"[FlexGloveBLEWrapper] Connection to {deviceName} failed or timed out");
        }
        
        onComplete?.Invoke(connected);
    }

    /// <summary>
    /// Step 5: On Device Connected - Start reading flex sensor values
    /// </summary>
    private void OnDeviceConnected()
    {
        Debug.Log("[FlexGloveBLEWrapper] Device connected! Starting flex sensor data reading coroutine...");
        
        // Stop any existing read coroutine
        if (readDataCoroutine != null)
        {
            StopCoroutine(readDataCoroutine);
        }
        
        // Start new coroutine that reads flex sensor values at defined interval
        readDataCoroutine = StartCoroutine(ReadFlexSensorValuesCoroutine());
    }

    /// <summary>
    /// Coroutine that runs at defined interval to read flex sensor values from BLE
    /// Reads thumb, index, middle, ring, pinky flex sensor values from ESP32
    /// </summary>
    private IEnumerator ReadFlexSensorValuesCoroutine()
    {
        Debug.Log($"[FlexGloveBLEWrapper] Starting flex sensor value reader (interval: {readIntervalSeconds}s)");
        
        // Ensure we start with connected state
        if (bleReader != null)
        {
            IsConnected = bleReader.IsConnected;
        }
        
        while (IsConnected && bleReader != null && bleReader.IsConnected)
        {
            // Read flex sensor values from the reader
            // These are analog values from the flex sensors (typically 0-1023 for 10-bit ADC)
            thumb = bleReader.thumb;
            index = bleReader.index;
            middle = bleReader.middle;
            ring = bleReader.ring;
            pinky = bleReader.pinky;
            
            // Read temperature value (raw, not normalized)
            temperature = bleReader.temperature;
            
            // Update connection status
            IsConnected = bleReader.IsConnected;
            connectionStatus = IsConnected ? "Connected - Reading Flex Sensor Data" : "Disconnected";
            
            // Wait for next read interval
            yield return new WaitForSeconds(readIntervalSeconds);
        }
        
        Debug.Log("[FlexGloveBLEWrapper] Stopped reading flex sensor values");
        connectionStatus = "Disconnected";
        IsConnected = false;
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Update connection status from reader if available
        if (bleReader != null && isInitialized)
        {
            IsConnected = bleReader.IsConnected;
            if (!IsConnected && connectionStatus.Contains("Connected"))
            {
                connectionStatus = "Disconnected";
                // Stop reading coroutine if connection lost
                if (readDataCoroutine != null)
                {
                    StopCoroutine(readDataCoroutine);
                    readDataCoroutine = null;
                }
            }
        }
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (readDataCoroutine != null)
        {
            StopCoroutine(readDataCoroutine);
        }
        
        if (bleReader != null)
        {
            bleReader.Disconnect();
        }
        
        if (bridge != null)
        {
            bridge.CloseBlocking();
        }
#endif
    }

    /// <summary>
    /// Public method to manually query devices
    /// </summary>
    public void QueryDevices()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge == null)
        {
            bridge = new FlexGloveAndroidBridge();
            bridge.Init();
        }
        
        StartCoroutine(QueryAvailableBluetoothDevices(
            (devices) =>
            {
                scannedDevices = devices;
                Debug.Log($"[FlexGloveBLEWrapper] Manual query found {devices.Length} device(s)");
            },
            (error) =>
            {
                Debug.LogError($"[FlexGloveBLEWrapper] Manual query failed: {error}");
            }
        ));
#else
        Debug.LogWarning("[FlexGloveBLEWrapper] QueryDevices only works on Android/Quest");
#endif
    }
#else
    /// <summary>
    /// Initialize Serial communication for PC/Editor
    /// </summary>
    private void InitializeSerial()
    {
        serialReader = gameObject.AddComponent<FlexGloveSerialReader>();
        serialReader.portName = serialPortName;
        serialReader.baudRate = 115200;
        Debug.Log($"[FlexGloveBLEWrapper] Using Serial communication on {serialPortName}");
    }

    void Update()
    {
        if (serialReader != null)
        {
            thumb = serialReader.thumb;
            index = serialReader.index;
            middle = serialReader.middle;
            ring = serialReader.ring;
            pinky = serialReader.pinky;
            IsConnected = true; // Assume connected if reader exists
        }
    }
#endif
}
