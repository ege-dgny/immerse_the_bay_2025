using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FlexGloveUIWithBars : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the FlexGloveSerialReader component")]
    public FlexGloveSerialReader serialReader;

    [Header("UI Elements - Text")]
    public TextMeshProUGUI thumbText;
    public TextMeshProUGUI indexText;
    public TextMeshProUGUI middleText;
    public TextMeshProUGUI ringText;
    public TextMeshProUGUI pinkyText;

    [Header("UI Elements - Progress Bars")]
    [Tooltip("Image components with Fill type for visual bars")]
    public Image thumbBar;
    public Image indexBar;
    public Image middleBar;
    public Image ringBar;
    public Image pinkyBar;

    [Header("Display Settings")]
    public string[] fingerLabels = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    
    [Tooltip("Maximum sensor value for normalization (adjust based on your sensor range)")]
    public int maxSensorValue = 1023;
    
    [Tooltip("Update rate in seconds (0 = every frame)")]
    public float updateInterval = 0.1f;

    private float lastUpdateTime = 0f;

    void Start()
    {
        // Try to find the serial reader if not assigned
        if (serialReader == null)
        {
            serialReader = FindFirstObjectByType<FlexGloveSerialReader>();
            if (serialReader == null)
            {
                Debug.LogWarning("FlexGloveUIWithBars: FlexGloveSerialReader not found! Please assign it in the inspector.");
            }
        }
    }

    void Update()
    {
        if (serialReader == null) return;

        // Throttle updates if needed
        if (updateInterval > 0 && Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        // Update thumb
        UpdateFingerDisplay(serialReader.thumb, thumbText, thumbBar, 0);

        // Update index
        UpdateFingerDisplay(serialReader.index, indexText, indexBar, 1);

        // Update middle
        UpdateFingerDisplay(serialReader.middle, middleText, middleBar, 2);

        // Update ring
        UpdateFingerDisplay(serialReader.ring, ringText, ringBar, 3);

        // Update pinky
        UpdateFingerDisplay(serialReader.pinky, pinkyText, pinkyBar, 4);
    }

    private void UpdateFingerDisplay(int sensorValue, TextMeshProUGUI textElement, Image barElement, int fingerIndex)
    {
        // Update text
        if (textElement != null)
        {
            textElement.text = $"{fingerLabels[fingerIndex]}: {sensorValue}";
        }

        // Update progress bar
        if (barElement != null)
        {
            float normalizedValue = Mathf.Clamp01((float)sensorValue / maxSensorValue);
            barElement.fillAmount = normalizedValue;
        }
    }
}

