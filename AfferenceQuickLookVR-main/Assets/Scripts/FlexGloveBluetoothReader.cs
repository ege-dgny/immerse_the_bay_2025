using UnityEngine;
using System;
using System.Collections;
using System.Text;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Reads flex sensor data from ESP32 via Bluetooth Low Energy (BLE)
/// Uses FlexGloveAndroidBridge for Android/Quest, falls back to Serial on PC/Editor
/// </summary>
public class FlexGloveBluetoothReader : MonoBehaviour
{
    [Header("BLE Settings")]
    [Tooltip("BLE Device name to search for (must match ESP32 device name)")]
    public string deviceName = "FlexGlove-ESP32";

    [Header("Connection Settings")]
    [Tooltip("Auto-query devices on Start (instead of auto-connect)")]
    public bool autoConnect = true;

    [Tooltip("Connection timeout in milliseconds")]
    public int connectionTimeoutMs = 10000;

    [Tooltip("Scan duration in milliseconds for device query")]
    public int scanDurationMs = 5000;

    // Parsed sensor values (same interface as SerialReader)
    public int thumb;
    public int index;
    public int middle;
    public int ring;
    public int pinky;

    // Temperature value (raw, not normalized)
    public int temperature;

    // Connection status
    public bool IsConnected { get; private set; }
    public string ConnectionStatus { get; private set; } = "Not Connected";

    // Scanned devices list
    public string[] ScannedDevices { get; private set; } = new string[0];

#if UNITY_ANDROID && !UNITY_EDITOR
    private FlexGloveAndroidBridge bridge;
    private StringBuilder dataBuffer = new StringBuilder();
    private bool isConnecting = false;
    private float lastDataTime = 0f;
    private float lastBufferLogTime = 0f;
#endif

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge = new FlexGloveAndroidBridge();
        if (autoConnect)
        {
            StartCoroutine(RequestPermissionsAndQueryDevices());
        }
#else
        Debug.LogWarning("FlexGloveBluetoothReader: BLE only works on Android. Use FlexGloveSerialReader for PC/Editor.");
        ConnectionStatus = "BLE not available on this platform";
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator RequestPermissionsAndQueryDevices()
    {
        // Request BLE permissions
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
        {
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
            yield return new WaitForSeconds(1f);
        }

        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
        {
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(0.5f);
        QueryDevices(scanDurationMs);
    }
    
    /// <summary>
    /// Query for available BLE devices
    /// </summary>
    /// <param name="durationMs">How long to scan in milliseconds (uses scanDurationMs if not specified)</param>
    public void QueryDevices(int? durationMs = null)
    {
        if (bridge == null)
        {
            Debug.LogError("[FlexGloveBLE] Bridge not initialized");
            ConnectionStatus = "Bridge not initialized";
            return;
        }
        
        int duration = durationMs ?? scanDurationMs;
        ConnectionStatus = "Scanning for devices...";
        Debug.Log($"[FlexGloveBLE] Starting device scan for {duration}ms");
        
        bridge.ScanForDevices(
            duration,
            onResult: (devices) =>
            {
                ScannedDevices = devices;
                ConnectionStatus = $"Found {devices.Length} device(s)";
                Debug.Log($"[FlexGloveBLE] Scan complete! Found {devices.Length} device(s):");
                foreach (var device in devices)
                {
                    Debug.Log($"  - {device}");
                }
            },
            onError: (error) =>
            {
                ConnectionStatus = $"Scan failed: {error}";
                Debug.LogError($"[FlexGloveBLE] Device scan failed: {error}");
                ScannedDevices = new string[0];
            }
        );
    }

    public void Connect()
    {
        if (isConnecting || IsConnected)
            return;

        StartCoroutine(ConnectCoroutine());
    }

    private IEnumerator ConnectCoroutine()
    {
        if (isConnecting || IsConnected)
            yield break;

        isConnecting = true;
        ConnectionStatus = "Connecting...";
        Debug.Log($"[FlexGloveBLE] Connecting to {deviceName}...");

        // Connect via bridge
        bool connected = bridge.OpenBlocking(deviceName, connectionTimeoutMs);

        if (connected)
        {
            IsConnected = true;
            ConnectionStatus = "Connected";
            Debug.Log($"[FlexGloveBLE] Connected to {deviceName}");
        }
        else
        {
            ConnectionStatus = "Connection failed";
            Debug.LogError($"[FlexGloveBLE] Failed to connect to {deviceName}");
        }

        isConnecting = false;
    }

    private void ProcessReceivedData(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        // Convert bytes to string
        string text = Encoding.ASCII.GetString(data);
        // Debug.Log($"[FlexGloveBLE] Received data ({data.Length} bytes): {text}"); // COMMENTED: Too noisy - receiving data constantly
        dataBuffer.Append(text);

        string buffer = dataBuffer.ToString();

        // Process complete lines (ending with \n) or complete messages
        bool processed = false;
        
        while (buffer.Contains("\n"))
        {
            int newlineIndex = buffer.IndexOf("\n");
            string line = buffer.Substring(0, newlineIndex).Trim();
            buffer = buffer.Substring(newlineIndex + 1);
            
            if (TryParseFlexSensorData(line))
            {
                processed = true;
            }
        }
        
        // If no newlines, try to parse the entire buffer as a single message
        // (ESP32 sends complete messages without newlines, but BLE splits them across packets)
        // Wait for complete flex sensor message: must have all 5 finger markers (T:, I:, M:, R:, P:)
        if (!processed && buffer.Length > 0)
        {
            // Check if we have all 5 finger markers (complete message)
            bool hasT = buffer.Contains("T:");
            bool hasI = buffer.Contains("I:");
            bool hasM = buffer.Contains("M:");
            bool hasR = buffer.Contains("R:");
            bool hasP = buffer.Contains("P:");
            
            if (hasT && hasI && hasM && hasR && hasP)
            {
                // We have all markers, try to parse
                string trimmed = buffer.Trim();
                if (TryParseFlexSensorData(trimmed))
                {
                    buffer = ""; // Clear buffer after successful parse
                    processed = true;
                }
                else
                {
                    // Parsing failed even though we have all markers - might be malformed
                    // Clear buffer to prevent infinite accumulation
                    // Debug.LogWarning($"[FlexGloveBLE] Failed to parse complete message, clearing buffer: {trimmed}"); // COMMENTED: Too noisy - parsing errors happen frequently
                    buffer = "";
                }
            }
            // If we don't have all markers yet, keep accumulating in buffer
            else if (hasT || hasI || hasM || hasR || hasP)
            {
                // We have some markers but not all - waiting for more data
                // Only log occasionally to avoid spam (every ~1 second)
                if (Time.time - lastBufferLogTime > 1f)
                {
                    Debug.Log($"[FlexGloveBLE] Waiting for complete message. Buffer ({buffer.Length} chars): {buffer.Substring(0, Math.Min(50, buffer.Length))}... Has: T={hasT} I={hasI} M={hasM} R={hasR} P={hasP}");
                    lastBufferLogTime = Time.time;
                }
            }
            
            // But limit buffer size to prevent memory issues (max ~200 chars should be enough)
            if (buffer.Length > 200)
            {
                // Debug.LogWarning($"[FlexGloveBLE] Buffer too large ({buffer.Length} chars), clearing: {buffer.Substring(0, 50)}..."); // COMMENTED: Too noisy
                buffer = "";
            }
        }

        // Keep remaining buffer for next read
        dataBuffer.Clear();
        dataBuffer.Append(buffer);
    }
    
    /// <summary>
    /// Try to parse flex sensor data from a line of text
    /// Supports both CSV format (thumb,index,middle,ring,pinky) and ESP32 format (Flex: T:123 I:456...)
    /// </summary>
    private bool TryParseFlexSensorData(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;
        
        // Try CSV format first: thumb,index,middle,ring,pinky
        string[] parts = line.Split(',');
        if (parts.Length == 5)
        {
            if (int.TryParse(parts[0], out int t) &&
                int.TryParse(parts[1], out int i) &&
                int.TryParse(parts[2], out int m) &&
                int.TryParse(parts[3], out int r) &&
                int.TryParse(parts[4], out int p))
            {
                thumb = t;
                index = i;
                middle = m;
                ring = r;
                pinky = p;
                lastDataTime = Time.time;
                return true;
            }
        }
        
        // Try ESP32 format: "Flex: T:123 I:456 M:789 R:234 P:567 Th:14565" or "T:123I:456M:789R:234P:567Th:14565" (without spaces/prefix)
        // Handle both with and without "Flex: " prefix, and with/without spaces
        // Look for pattern: T:### I:### M:### R:### P:### Th:### (temperature is treated as 6th sensor)
        
        // Find all finger markers (handle both " T:" and "T:", " I:" and "I:", etc.)
        int tIndex = line.IndexOf("T:");
        int iIndex = -1, mIndex = -1, rIndex = -1, pIndex = -1, thIndex = -1;
        
        // Try with spaces first: " I:", " M:", " R:", " P:", " Th:"
        iIndex = line.IndexOf(" I:");
        if (iIndex < 0) iIndex = line.IndexOf("I:"); // Fallback to no space
        
        mIndex = line.IndexOf(" M:");
        if (mIndex < 0) mIndex = line.IndexOf("M:");
        
        rIndex = line.IndexOf(" R:");
        if (rIndex < 0) rIndex = line.IndexOf("R:");
        
        pIndex = line.IndexOf(" P:");
        if (pIndex < 0) pIndex = line.IndexOf("P:");
        
        // Temperature marker (must come after P to avoid confusion with T:)
        thIndex = line.IndexOf(" Th:");
        if (thIndex < 0) thIndex = line.IndexOf("Th:");
        
        // We need all 5 finger markers and they must be in order (temperature is optional)
        if (tIndex >= 0 && iIndex > tIndex && mIndex > iIndex && rIndex > mIndex && pIndex > rIndex)
        {
            try
            {
                // Extract T value (from "T:" to start of I)
                string tStr = ExtractValue(line, tIndex + 2, iIndex);
                
                // Extract I value (from "I:" to start of M, handle both " I:" and "I:")
                int iStart = (line[iIndex] == ' ') ? iIndex + 3 : iIndex + 2;
                string iStr = ExtractValue(line, iStart, mIndex);
                
                // Extract M value
                int mStart = (line[mIndex] == ' ') ? mIndex + 3 : mIndex + 2;
                string mStr = ExtractValue(line, mStart, rIndex);
                
                // Extract R value
                int rStart = (line[rIndex] == ' ') ? rIndex + 3 : rIndex + 2;
                string rStr = ExtractValue(line, rStart, pIndex);
                
                // Extract P value (from "P:" to start of Th: or end)
                int pStart = (line[pIndex] == ' ') ? pIndex + 3 : pIndex + 2;
                string pStr;
                if (thIndex > pIndex)
                {
                    pStr = ExtractValue(line, pStart, thIndex);
                }
                else
                {
                    pStr = line.Substring(pStart).Trim();
                }
                
                // Extract temperature value if present (from "Th:" to end)
                string thStr = "";
                if (thIndex > pIndex)
                {
                    int thStart = (line[thIndex] == ' ') ? thIndex + 4 : thIndex + 3;
                    thStr = line.Substring(thStart).Trim();
                }
                
                // Try to parse all values
                if (int.TryParse(tStr, out int t) &&
                    int.TryParse(iStr, out int i) &&
                    int.TryParse(mStr, out int m) &&
                    int.TryParse(rStr, out int r) &&
                    int.TryParse(pStr, out int p))
                {
                    thumb = t;
                    index = i;
                    middle = m;
                    ring = r;
                    pinky = p;
                    
                    // Parse temperature if present
                    if (!string.IsNullOrEmpty(thStr) && int.TryParse(thStr, out int temp))
                    {
                        temperature = temp;
                    }
                    
                    lastDataTime = Time.time;
                    // Debug.Log($"[FlexGloveBLE] Parsed flex values - T:{thumb} I:{index} M:{middle} R:{ring} P:{pinky} Th:{temperature}"); // COMMENTED: Too noisy - parsing values constantly
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FlexGloveBLE] Parsing exception: {ex.Message} for line: {line}");
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Extract a numeric value from a string between start index and end index
    /// Handles spaces and non-digit characters at boundaries
    /// </summary>
    private string ExtractValue(string line, int start, int end)
    {
        if (start >= end || start < 0 || end > line.Length)
            return "";
        
        // Extract substring
        string segment = line.Substring(start, end - start);
        
        // Remove any trailing spaces or non-digits
        segment = segment.Trim();
        
        // Find the last digit (in case there are non-digit chars after the number)
        int lastDigit = -1;
        for (int i = segment.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(segment[i]))
            {
                lastDigit = i;
                break;
            }
        }
        
        if (lastDigit >= 0)
        {
            return segment.Substring(0, lastDigit + 1);
        }
        
        return segment;
    }
#endif

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsConnected && bridge != null)
        {
            // Read data from bridge
            byte[] data = bridge.RxBlocking(50); // 50ms timeout
            if (data != null && data.Length > 0)
            {
                ProcessReceivedData(data);
            }
            else if (IsConnected && Time.time - lastDataTime > 2f && lastDataTime > 0)
            {
                // Log warning if no data received for 2 seconds
                Debug.LogWarning($"[FlexGloveBLE] No data received for {Time.time - lastDataTime:F1}s (connected: {IsConnected}, bridge open: {bridge.IsOpen})");
                lastDataTime = Time.time; // Reset to avoid spam
            }

            // Check connection status
            if (!bridge.IsOpen && IsConnected)
            {
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                Debug.LogWarning("[FlexGloveBLE] Connection lost");
            }
        }
#endif
    }

    public void Disconnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge != null)
        {
            bridge.CloseBlocking();
        }
        IsConnected = false;
        ConnectionStatus = "Disconnected";
        Debug.Log("[FlexGloveBLE] Disconnected");
#endif
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }
}

