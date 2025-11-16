using UnityEngine;
using System.IO.Ports;
using System;

public class FlexGloveSerialReader : MonoBehaviour
{
    [Header("Serial Port Settings")]
    public string portName = "COM5";   // change if your ESP32 is on a different COM
    public int baudRate = 115200;

    private SerialPort serialPort;
    private string latestLine = "";

    // Parsed values
    public int thumb;
    public int index;
    public int middle;
    public int ring;
    public int pinky;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50; // ms
            serialPort.Open();
            Debug.Log($"Opened serial port {portName} at {baudRate} baud.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to open serial port {portName}: {e.Message}");
        }
    }

    void Update()
    {
        if (serialPort == null || !serialPort.IsOpen) return;

        try
        {
            // Read one line from ESP32 (thumb,index,middle,ring,pinky)
            string line = serialPort.ReadLine().Trim();
            latestLine = line;

            // Log raw line for testing
            Debug.Log($"Glove raw: {line}");

            // Parse CSV
            string[] parts = line.Split(',');
            if (parts.Length == 5)
            {
                thumb  = int.Parse(parts[0]);
                index  = int.Parse(parts[1]);
                middle = int.Parse(parts[2]);
                ring   = int.Parse(parts[3]);
                pinky  = int.Parse(parts[4]);

                // Optional: log parsed values nicely
                // Debug.Log($"Thumb:{thumb} Index:{index} Middle:{middle} Ring:{ring} Pinky:{pinky}");
            }
        }
        catch (TimeoutException)
        {
            // No data this frame, ignore
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Serial read error: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("Closed serial port.");
        }
    }
}
