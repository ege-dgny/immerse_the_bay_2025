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

    [Header("Thermistor Settings")]
    [Tooltip("Enable temperature conversion to Celsius")]
    public bool convertTemperatureToCelsius = true;

    [Tooltip("ADC resolution (e.g., 4096 for 12-bit, 65536 for 16-bit). Set to 0 if reading is already resistance in ohms.")]
    public int adcResolution = 65536;

    [Tooltip("Fixed resistor value in ohms (10kΩ = 10000)")]
    public float fixedResistorOhms = 10000f;

    [Tooltip("Thermistor Beta value (typical 10kΩ NTC thermistor: 3950K). Calibrated: 3950K")]
    public float thermistorBeta = 3950f;

    [Tooltip("Reference temperature in Celsius (typically 25°C). Calibrated: 25°C")]
    public float referenceTempCelsius = 25f;

    [Tooltip("Thermistor resistance at reference temperature in ohms (10kΩ thermistor at 25°C = 10000). Calibrated: 10000Ω")]
    public float referenceResistanceOhms = 10000f;

    [Tooltip("Temperature calibration offset in Celsius. Adjust this to match actual temperature. Example: If showing -1.1°C but actual is 23°C, set offset to 24.1")]
    public float temperatureOffsetCelsius = 24.1f;

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
    /// Get the current converted temperature in Celsius
    /// </summary>
    /// <returns>Temperature in Celsius, or 0 if conversion is disabled or wrapper is null</returns>
    public float GetCurrentTemperatureCelsius()
    {
        if (bleWrapper == null) return 0f;
        
        if (convertTemperatureToCelsius)
        {
            return ConvertThermistorToCelsius(bleWrapper.temperature);
        }
        else
        {
            return bleWrapper.temperature; // Return raw value if conversion disabled
        }
    }

    /// <summary>
    /// Convert thermistor ADC reading to Celsius temperature
    /// Uses Beta equation: 1/T = 1/T0 + (1/Beta) * ln(R/R0)
    /// 
    /// Calibration values:
    /// - R0 = 10000 ohm (reference resistor at 25°C)
    /// - T0 = 25°C = 298.15K
    /// - Beta = 3950K
    /// - Example: R = 12966 ohm at room temp (12750 ADC reading)
    /// </summary>
    /// <param name="adcReading">Raw ADC reading from thermistor</param>
    /// <returns>Temperature in Celsius</returns>
    public float ConvertThermistorToCelsius(int adcReading)
    {
        float thermistorResistance;

        if (adcResolution > 0)
        {
            // Convert ADC reading to resistance using voltage divider formula
            // Voltage divider: Vout = Vref * R_fixed / (R_thermistor + R_fixed)
            // ADC_reading = (Vout / Vref) * ADC_resolution
            // Solving for R_thermistor:
            // R_thermistor = R_fixed * (1 - voltageRatio) / voltageRatio
            float voltageRatio = (float)adcReading / adcResolution;
            
            // Handle edge cases
            if (voltageRatio >= 0.999f) return -100f; // Too high, likely open circuit
            if (voltageRatio <= 0.001f) return 200f;   // Too low, likely short circuit
            
            // Calculate thermistor resistance from voltage divider
            thermistorResistance = fixedResistorOhms * (1f - voltageRatio) / voltageRatio;
        }
        else
        {
            // Reading is already resistance in ohms
            thermistorResistance = adcReading;
        }

        // Convert resistance to temperature using Beta equation
        // Beta equation: 1/T = 1/T0 + (1/Beta) * ln(R/R0)
        // Solving for T: T = 1 / (1/T0 + (1/Beta) * ln(R/R0))
        // Where:
        // - T0 = reference temperature in Kelvin (25°C = 298.15K)
        // - R0 = reference resistance at T0 (10000 ohm)
        // - Beta = thermistor Beta value (3950K)
        // - R = current thermistor resistance
        
        float t0Kelvin = referenceTempCelsius + 273.15f; // Convert reference temp to Kelvin
        float lnRatio = Mathf.Log(thermistorResistance / referenceResistanceOhms);
        float invT = (1f / t0Kelvin) + (1f / thermistorBeta) * lnRatio;
        float tempKelvin = 1f / invT;
        float tempCelsius = tempKelvin - 273.15f;

        // Apply calibration offset
        tempCelsius += temperatureOffsetCelsius;

        return tempCelsius;
    }

    /// <summary>
    /// Get the finger state based on sensor value
    /// </summary>
    /// <param name="sensorValue">Current sensor reading (0-4095)</param>
    /// <returns>Tuple containing state name and color</returns>
    private (string state, Color color) GetFingerState(int sensorValue)
    {
        if (sensorValue >= 500 && sensorValue <= 4095)
        {
            return ("Relaxed", Color.green);
        }
        else if (sensorValue >= 150 && sensorValue <= 499)
        {
            return ("Contracted", new Color(1f, 0.647f, 0f)); // Orange color
        }
        else // 0-149
        {
            return ("Overstressed", Color.red);
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

        // Update text displays with state-based information
        if (thumbText != null)
        {
            var (state, color) = GetFingerState(thumbVal);
            thumbText.text = $"{fingerLabels[0]}: {state}";
            thumbText.color = color;
        }

        if (indexText != null)
        {
            var (state, color) = GetFingerState(indexVal);
            indexText.text = $"{fingerLabels[1]}: {state}";
            indexText.color = color;
        }

        if (middleText != null)
        {
            var (state, color) = GetFingerState(middleVal);
            middleText.text = $"{fingerLabels[2]}: {state}";
            middleText.color = color;
        }

        if (ringText != null)
        {
            var (state, color) = GetFingerState(ringVal);
            ringText.text = $"{fingerLabels[3]}: {state}";
            ringText.color = color;
        }

        if (pinkyText != null)
        {
            var (state, color) = GetFingerState(pinkyVal);
            pinkyText.text = $"{fingerLabels[4]}: {state}";
            pinkyText.color = color;
        }

        if (temperatureText != null)
        {
            if (convertTemperatureToCelsius)
            {
                float tempCelsius = ConvertThermistorToCelsius(temperatureVal);
                temperatureText.text = $"Temperature: {tempCelsius:F1}°C";
            }
            else
            {
                temperatureText.text = $"Temperature: {temperatureVal}";
            }
        }

        // Scale cubes based on sensor values (inverse: lower value = taller cube)
        // Works with either direct cube assignment or parent-child structure
        // DISABLED: Comment out cube scaling to use text display only
        /*
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
        */
    }
}

