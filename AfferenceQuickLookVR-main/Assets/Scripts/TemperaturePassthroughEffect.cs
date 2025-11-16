using UnityEngine;

/// <summary>
/// Changes passthrough edge color based on temperature readings from FlexGlove
/// - Below 15°C: Frost blue pulsing
/// - Above 50°C: Fire red pulsing
/// </summary>
public class TemperaturePassthroughEffect : MonoBehaviour
{
    [Header("Passthrough Reference")]
    [Tooltip("Reference to OVRPassthroughLayer. Will auto-find if not assigned.")]
    public OVRPassthroughLayer passthrough;

    [Header("Temperature Source")]
    [Tooltip("Reference to FlexGloveBLEWrapper for temperature data. Will auto-find if not assigned.")]
    public FlexGloveBLEWrapper flexGloveWrapper;

    [Tooltip("Reference to FlexGloveUIDisplay for converted temperature. Optional - will convert manually if not assigned.")]
    public FlexGloveUIDisplay flexGloveUIDisplay;

    [Header("Temperature Thresholds")]
    [Tooltip("Temperature in Celsius below which cold warning activates (frost blue)")]
    public float coldThreshold = 15f;

    [Tooltip("Temperature in Celsius above which hot warning activates (fire red)")]
    public float hotThreshold = 50f;

    [Header("Cold Warning Settings")]
    [Tooltip("Frost blue color for cold temperature warning")]
    public Color coldColor = new Color(0.4f, 0.7f, 1f); // Frost blue

    [Tooltip("Pulse frequency for cold warning (cycles per second)")]
    public float coldPulseFrequency = 1.5f;

    [Tooltip("Pulse intensity for cold warning (0-1, how much the color varies)")]
    [Range(0f, 1f)]
    public float coldPulseIntensity = 0.3f;

    [Header("Hot Warning Settings")]
    [Tooltip("Fire red color for hot temperature warning")]
    public Color hotColor = new Color(1f, 0.2f, 0f); // Fire red

    [Tooltip("Pulse frequency for hot warning (cycles per second)")]
    public float hotPulseFrequency = 2f;

    [Tooltip("Pulse intensity for hot warning (0-1, how much the color varies)")]
    [Range(0f, 1f)]
    public float hotPulseIntensity = 0.4f;

    [Header("Normal State")]
    [Tooltip("Normal passthrough color when temperature is in safe range")]
    public Color normalColor = Color.white;

    [Header("Thermistor Conversion (if not using FlexGloveUIDisplay)")]
    [Tooltip("Enable manual temperature conversion if FlexGloveUIDisplay is not available")]
    public bool useManualConversion = false;

    [Tooltip("ADC resolution (e.g., 65536 for 16-bit). Only used if useManualConversion is true.")]
    public int adcResolution = 65536;

    [Tooltip("Fixed resistor value in ohms (10kΩ = 10000). Only used if useManualConversion is true.")]
    public float fixedResistorOhms = 10000f;

    [Tooltip("Thermistor Beta value (3950K). Only used if useManualConversion is true.")]
    public float thermistorBeta = 3950f;

    [Tooltip("Reference temperature in Celsius (25°C). Only used if useManualConversion is true.")]
    public float referenceTempCelsius = 25f;

    [Tooltip("Reference resistance at reference temp (10000Ω). Only used if useManualConversion is true.")]
    public float referenceResistanceOhms = 10000f;

    [Tooltip("Temperature calibration offset. Only used if useManualConversion is true.")]
    public float temperatureOffsetCelsius = 24.1f;

    // Internal state
    private float pulseTimer = 0f;
    private float currentTemperature = 25f; // Default to room temp

    void Start()
    {
        // Find passthrough layer if not assigned
        if (passthrough == null)
        {
            passthrough = FindFirstObjectByType<OVRPassthroughLayer>();
            if (passthrough == null)
            {
                Debug.LogError("[TemperaturePassthroughEffect] OVRPassthroughLayer not found! Please assign it in the Inspector.");
            }
        }

        // Find FlexGloveWrapper if not assigned
        if (flexGloveWrapper == null)
        {
            flexGloveWrapper = FindFirstObjectByType<FlexGloveBLEWrapper>();
            if (flexGloveWrapper == null)
            {
                Debug.LogWarning("[TemperaturePassthroughEffect] FlexGloveBLEWrapper not found. Temperature effects will not work.");
            }
        }

        // Find FlexGloveUIDisplay if not assigned (optional)
        if (flexGloveUIDisplay == null)
        {
            flexGloveUIDisplay = FindFirstObjectByType<FlexGloveUIDisplay>();
        }

        // Enable color scale override
        if (passthrough != null)
        {
            passthrough.overridePerLayerColorScaleAndOffset = true;
        }
    }

    void Update()
    {
        if (passthrough == null || flexGloveWrapper == null) return;

        // Get current temperature
        currentTemperature = GetCurrentTemperature();

        // Determine which warning state we're in
        bool isCold = currentTemperature < coldThreshold;
        bool isHot = currentTemperature > hotThreshold;
        bool isNormal = !isCold && !isHot;

        // Update pulse timer
        pulseTimer += Time.deltaTime;

        // Calculate pulse effect (sine wave from 0 to 1)
        float pulseValue = 0f;
        float pulseIntensity = 0f;
        Color targetColor = normalColor;

        if (isCold)
        {
            // Cold warning: frost blue pulsing
            float pulsePhase = pulseTimer * coldPulseFrequency * 2f * Mathf.PI;
            pulseValue = (Mathf.Sin(pulsePhase) + 1f) * 0.5f; // 0 to 1
            pulseIntensity = pulseValue * coldPulseIntensity;

            // Pulse between normal and cold color
            targetColor = Color.Lerp(normalColor, coldColor, 0.5f + pulseIntensity);
        }
        else if (isHot)
        {
            // Hot warning: fire red pulsing
            float pulsePhase = pulseTimer * hotPulseFrequency * 2f * Mathf.PI;
            pulseValue = (Mathf.Sin(pulsePhase) + 1f) * 0.5f; // 0 to 1
            pulseIntensity = pulseValue * hotPulseIntensity;

            // Pulse between normal and hot color
            targetColor = Color.Lerp(normalColor, hotColor, 0.5f + pulseIntensity);
        }
        else
        {
            // Normal state: no pulsing
            targetColor = normalColor;
            pulseTimer = 0f; // Reset timer when normal
        }

        // Apply color to passthrough
        ApplyPassthroughColor(targetColor, pulseIntensity, isCold || isHot);
    }

    /// <summary>
    /// Get current temperature in Celsius
    /// </summary>
    private float GetCurrentTemperature()
    {
        if (flexGloveUIDisplay != null)
        {
            // Use converted temperature from FlexGloveUIDisplay if available
            return flexGloveUIDisplay.GetCurrentTemperatureCelsius();
        }
        else if (useManualConversion)
        {
            // Manual conversion using this script's parameters
            return ConvertThermistorToCelsius(flexGloveWrapper.temperature);
        }
        else
        {
            // Fallback: return raw value (not ideal, but better than nothing)
            Debug.LogWarning("[TemperaturePassthroughEffect] No temperature conversion available. Assign FlexGloveUIDisplay or enable useManualConversion.");
            return flexGloveWrapper.temperature;
        }
    }

    /// <summary>
    /// Convert thermistor ADC reading to Celsius temperature
    /// Uses the same logic as FlexGloveUIDisplay
    /// </summary>
    private float ConvertThermistorToCelsius(int adcReading)
    {
        float thermistorResistance;

        if (adcResolution > 0)
        {
            // Convert ADC reading to resistance using voltage divider formula
            float voltageRatio = (float)adcReading / adcResolution;

            // Handle edge cases
            if (voltageRatio >= 0.999f) return -100f;
            if (voltageRatio <= 0.001f) return 200f;

            // Calculate thermistor resistance from voltage divider
            thermistorResistance = fixedResistorOhms * (1f - voltageRatio) / voltageRatio;
        }
        else
        {
            // Reading is already resistance in ohms
            thermistorResistance = adcReading;
        }

        // Convert resistance to temperature using Beta equation
        float t0Kelvin = referenceTempCelsius + 273.15f;
        float lnRatio = Mathf.Log(thermistorResistance / referenceResistanceOhms);
        float invT = (1f / t0Kelvin) + (1f / thermistorBeta) * lnRatio;
        float tempKelvin = 1f / invT;
        float tempCelsius = tempKelvin - 273.15f;

        // Apply calibration offset
        tempCelsius += temperatureOffsetCelsius;

        return tempCelsius;
    }

    /// <summary>
    /// Apply color to passthrough layer
    /// </summary>
    private void ApplyPassthroughColor(Color color, float pulseIntensity, bool isWarning)
    {
        // Set color scale (RGB multipliers)
        passthrough.colorScale = new Vector4(color.r, color.g, color.b, 1f);

        // Add color offset for additional intensity during pulsing
        if (isWarning)
        {
            // Enhance the color during pulse peaks
            Vector4 colorOffset = new Vector4(
                color.r * pulseIntensity * 0.2f,
                color.g * pulseIntensity * 0.2f,
                color.b * pulseIntensity * 0.2f,
                0f
            );
            passthrough.colorOffset = colorOffset;
        }
        else
        {
            // Normal state: no offset
            passthrough.colorOffset = Vector4.zero;
        }
    }

    /// <summary>
    /// Get current temperature (public method for debugging)
    /// </summary>
    public float GetTemperature()
    {
        return currentTemperature;
    }
}

