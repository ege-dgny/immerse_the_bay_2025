using UnityEngine;
using TMPro;

public class FlexGloveUIDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to FlexGloveBLEWrapper - automatically uses Serial (PC) or BLE (Quest)")]
    public FlexGloveBLEWrapper bleWrapper;

    [Header("UI Elements")]
    [Tooltip("Text element to display thumb sensor value")]
    public TextMeshProUGUI thumbText;

    [Tooltip("Text element to display index sensor value")]
    public TextMeshProUGUI indexText;

    [Tooltip("Text element to display middle sensor value")]
    public TextMeshProUGUI middleText;

    [Tooltip("Text element to display ring sensor value")]
    public TextMeshProUGUI ringText;

    [Tooltip("Text element to display pinky sensor value")]
    public TextMeshProUGUI pinkyText;

    [Tooltip("Text element to display temperature value")]
    public TextMeshProUGUI temperatureText;

    [Header("Cube Visualizations")]
    [Tooltip("Transform of the thumb cube (will scale on Y-axis). Can be direct cube or parent with child. Cube pivot Y should be 0 (bottom) for proper scaling.")]
    public Transform thumbCube;

    [Tooltip("Transform of the index cube (will scale on Y-axis). Can be direct cube or parent with child. Cube pivot Y should be 0 (bottom) for proper scaling.")]
    public Transform indexCube;

    [Tooltip("Transform of the middle cube (will scale on Y-axis). Can be direct cube or parent with child. Cube pivot Y should be 0 (bottom) for proper scaling.")]
    public Transform middleCube;

    [Tooltip("Transform of the ring cube (will scale on Y-axis). Can be direct cube or parent with child. Cube pivot Y should be 0 (bottom) for proper scaling.")]
    public Transform ringCube;

    [Tooltip("Transform of the pinky cube (will scale on Y-axis). Can be direct cube or parent with child. Cube pivot Y should be 0 (bottom) for proper scaling.")]
    public Transform pinkyCube;

    [Header("Display Settings")]
    [Tooltip("Prefix text before each sensor value (e.g., 'Thumb: ')")]
    public string[] fingerLabels = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    [Tooltip("Update rate in seconds (0 = every frame)")]
    public float updateInterval = 0.1f;

    [Header("Cube Scaling Settings")]
    [Tooltip("Sensor value when cube should be flat (default: 4095)")]
    public int flatValue = 4095;

    [Tooltip("Sensor value when cube should be at maximum height (default: 0)")]
    public int maxHeightValue = 0;

    [Tooltip("Maximum Y scale for the cubes when sensor is at maxHeightValue")]
    public float maxYScale = 5f;

    [Tooltip("Minimum Y scale for the cubes when sensor is at flatValue (default: 0.01 for nearly flat)")]
    public float minYScale = 0.01f;

    private float lastUpdateTime = 0f;

    void Start()
    {
        // Try to find wrapper if not assigned
        if (bleWrapper == null)
        {
            bleWrapper = FindFirstObjectByType<FlexGloveBLEWrapper>();
        }

        if (bleWrapper == null)
        {
            Debug.LogWarning("FlexGloveUIDisplay: FlexGloveBLEWrapper not found! Please assign FlexGloveManager (with FlexGloveBLEWrapper component) in the inspector.");
        }

        // Auto-find text components by name if not assigned
        AutoFindTextComponents();
    }

    /// <summary>
    /// Automatically find TextMeshProUGUI components by GameObject name
    /// </summary>
    private void AutoFindTextComponents()
    {
        // Find all TextMeshProUGUI components in the scene
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);

        foreach (var text in allTexts)
        {
            string objName = text.gameObject.name;
            
            if (thumbText == null && objName.Contains("Thumb", System.StringComparison.OrdinalIgnoreCase))
            {
                thumbText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found ThumbText: {objName}");
            }
            else if (indexText == null && objName.Contains("Index", System.StringComparison.OrdinalIgnoreCase))
            {
                indexText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found IndexText: {objName}");
            }
            else if (middleText == null && objName.Contains("Middle", System.StringComparison.OrdinalIgnoreCase))
            {
                middleText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found MiddleText: {objName}");
            }
            else if (ringText == null && objName.Contains("Ring", System.StringComparison.OrdinalIgnoreCase))
            {
                ringText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found RingText: {objName}");
            }
            else if (pinkyText == null && objName.Contains("Pinky", System.StringComparison.OrdinalIgnoreCase))
            {
                pinkyText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found PinkyText: {objName}");
            }
            else if (temperatureText == null && objName.Contains("Temperature", System.StringComparison.OrdinalIgnoreCase))
            {
                temperatureText = text;
                Debug.Log($"[FlexGloveUIDisplay] Auto-found TemperatureText: {objName}");
            }
        }

        // Log what we found
        if (thumbText == null || indexText == null || middleText == null || ringText == null || pinkyText == null)
        {
            Debug.LogWarning("[FlexGloveUIDisplay] Some text components not found. Please assign them manually in the inspector.");
        }
    }

    /// <summary>
    /// Calculate the Y scale for a cube based on sensor value
    /// Inverse relationship: lower sensor value = taller cube
    /// </summary>
    /// <param name="sensorValue">Current sensor reading (0-4095)</param>
    /// <returns>Y scale value between minYScale and maxYScale</returns>
    private float CalculateCubeScale(int sensorValue)
    {
        // Clamp sensor value to valid range
        int clampedValue = Mathf.Clamp(sensorValue, maxHeightValue, flatValue);
        
        // Calculate the range
        int valueRange = flatValue - maxHeightValue;
        
        // If range is 0, return min scale (shouldn't happen, but safety check)
        if (valueRange == 0)
            return minYScale;
        
        // Calculate normalized value (0 = maxHeightValue, 1 = flatValue)
        // Since we want inverse relationship, we invert it: lower sensor = taller cube
        float normalizedValue = 1f - ((float)(clampedValue - maxHeightValue) / valueRange);
        
        // Map normalized value to scale range
        float scale = Mathf.Lerp(minYScale, maxYScale, normalizedValue);
        
        return scale;
    }

    void Update()
    {
        if (bleWrapper == null) return;

        // Get sensor values from wrapper (handles Serial/BLE automatically)
        int thumbVal = bleWrapper.thumb;
        int indexVal = bleWrapper.index;
        int middleVal = bleWrapper.middle;
        int ringVal = bleWrapper.ring;
        int pinkyVal = bleWrapper.pinky;
        int temperatureVal = bleWrapper.temperature;

        // Throttle updates if needed
        if (updateInterval > 0 && Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        // Scale cubes based on sensor values (inverse: lower value = taller cube)
        // Works with either direct cube assignment or parent-child structure
        if (thumbCube != null)
        {
            Transform cubeToScale = thumbCube.childCount > 0 ? thumbCube.GetChild(0) : thumbCube;
            float yScale = CalculateCubeScale(thumbVal);
            Vector3 scale = cubeToScale.localScale;
            scale.y = yScale;
            cubeToScale.localScale = scale;
        }

        if (indexCube != null)
        {
            Transform cubeToScale = indexCube.childCount > 0 ? indexCube.GetChild(0) : indexCube;
            float yScale = CalculateCubeScale(indexVal);
            Vector3 scale = cubeToScale.localScale;
            scale.y = yScale;
            cubeToScale.localScale = scale;
        }

        if (middleCube != null)
        {
            Transform cubeToScale = middleCube.childCount > 0 ? middleCube.GetChild(0) : middleCube;
            float yScale = CalculateCubeScale(middleVal);
            Vector3 scale = cubeToScale.localScale;
            scale.y = yScale;
            cubeToScale.localScale = scale;
        }

        if (ringCube != null)
        {
            Transform cubeToScale = ringCube.childCount > 0 ? ringCube.GetChild(0) : ringCube;
            float yScale = CalculateCubeScale(ringVal);
            Vector3 scale = cubeToScale.localScale;
            scale.y = yScale;
            cubeToScale.localScale = scale;
        }

        if (pinkyCube != null)
        {
            Transform cubeToScale = pinkyCube.childCount > 0 ? pinkyCube.GetChild(0) : pinkyCube;
            float yScale = CalculateCubeScale(pinkyVal);
            Vector3 scale = cubeToScale.localScale;
            scale.y = yScale;
            cubeToScale.localScale = scale;
        }

        // Update temperature display (still using text)
        if (temperatureText != null)
            temperatureText.text = $"Temperature: {temperatureVal}";
    }
}

