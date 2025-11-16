using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AfferenceRingPairing : MonoBehaviour
{
    [Header("Ring Configuration")]
    [SerializeField] private string deviceId = "6f28";
    [SerializeField] private string userName = "ExampleUser";
    
    [Header("Buzz Settings")]
    [SerializeField] private float buzzInterval = 1.0f; // seconds between buzzes
    [SerializeField] private float buzzIntensity = 0.5f; // haptic intensity (0-1)
    [SerializeField] private float buzzDuration = 0.1f; // seconds each buzz lasts
    
    [Header("Auto Start")]
    [SerializeField] private bool autoStartOnEnable = true;
    
    private bool isConnected = false;
    private Coroutine buzzCoroutine;
    
    private void OnEnable()
    {
        if (autoStartOnEnable)
        {
            StartCoroutine(InitializeAndConnect());
        }
    }
    
    private void OnDisable()
    {
        StopBuzz();
    }
    
    /// <summary>
    /// Initialize and connect to the Afference ring
    /// </summary>
    public IEnumerator InitializeAndConnect()
    {
        if (HapticManager.Instance == null)
        {
            Debug.LogError("HapticManager.Instance is null! Make sure HapticManager exists in the scene.");
            yield break;
        }
        
        Debug.Log("Starting Afference Ring pairing...");
        
        // Step 1: Set device ID
        HapticManager.Instance.SetDevice(deviceId);
        Debug.Log($"Device set to: {deviceId}");
        
        // Step 2: Set communication type to BLE
        HapticManager.Instance.SetCommType("ble");
        Debug.Log("Communication type set to BLE");
        
        // Step 3: Load user (ExampleUser.json should be copied from StreamingAssets)
        HapticManager.Instance.LoadUser(userName);
        Debug.Log($"User loaded: {userName}");
        
        // Step 4: Wait a frame for user to load
        yield return null;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        // Step 5: Request BLE permissions (Android only)
        Debug.Log("Requesting BLE permissions...");
        Task<bool> permissionTask = HapticManager.Instance.EnsureBlePermissionsAsync();
        yield return new WaitUntil(() => permissionTask.IsCompleted);
        
        if (!permissionTask.Result)
        {
            Debug.LogError("BLE permissions denied!");
            yield break;
        }
        
        Debug.Log("BLE permissions granted");
        
        // Step 6: Wait for Android focus stability
        Task focusTask = HapticManager.Instance.WaitForAndroidFocusAndStabilityAsync();
        yield return new WaitUntil(() => focusTask.IsCompleted);
#endif
        
        // Step 7: Connect to the ring
        Debug.Log("Connecting to ring...");
        Task<bool> connectTask = HapticManager.Instance.ConnectCurrentUserAsync();
        yield return new WaitUntil(() => connectTask.IsCompleted);
        
        if (!connectTask.Result)
        {
            Debug.LogError($"Failed to connect: {HapticManager.Instance.status}");
            yield break;
        }
        
        Debug.Log("Successfully connected to Afference Ring!");
        isConnected = true;
        
        // Step 8: Start stimulation
        HapticManager.Instance.ToggleStim();
        Debug.Log("Stimulation started");
        
        // Step 9: Start periodic buzzing
        StartBuzz();
    }
    
    /// <summary>
    /// Start periodic buzzing
    /// </summary>
    public void StartBuzz()
    {
        if (!isConnected)
        {
            Debug.LogWarning("Ring not connected. Cannot start buzz.");
            return;
        }
        
        if (buzzCoroutine != null)
        {
            StopCoroutine(buzzCoroutine);
        }
        
        buzzCoroutine = StartCoroutine(BuzzCoroutine());
        Debug.Log($"Started periodic buzz (interval: {buzzInterval}s, intensity: {buzzIntensity})");
    }
    
    /// <summary>
    /// Stop periodic buzzing
    /// </summary>
    public void StopBuzz()
    {
        if (buzzCoroutine != null)
        {
            StopCoroutine(buzzCoroutine);
            buzzCoroutine = null;
        }
        
        // Send zero to stop any ongoing haptic
        if (HapticManager.Instance != null && HapticManager.Instance.stimActive)
        {
            HapticManager.Instance.SendHaptic(0f);
        }
    }
    
    /// <summary>
    /// Coroutine that sends periodic buzzes
    /// </summary>
    private IEnumerator BuzzCoroutine()
    {
        while (isConnected && HapticManager.Instance != null && HapticManager.Instance.stimActive)
        {
            // Send buzz
            HapticManager.Instance.SendHaptic(buzzIntensity);
            yield return new WaitForSeconds(buzzDuration);
            
            // Stop buzz
            HapticManager.Instance.SendHaptic(0f);
            yield return new WaitForSeconds(buzzInterval - buzzDuration);
        }
    }
    
    /// <summary>
    /// Manually trigger a single buzz
    /// </summary>
    public void TriggerBuzz()
    {
        if (!isConnected || HapticManager.Instance == null || !HapticManager.Instance.stimActive)
        {
            Debug.LogWarning("Cannot trigger buzz: ring not connected or stimulation not active");
            return;
        }
        
        StartCoroutine(SingleBuzz());
    }
    
    private IEnumerator SingleBuzz()
    {
        HapticManager.Instance.SendHaptic(buzzIntensity);
        yield return new WaitForSeconds(buzzDuration);
        HapticManager.Instance.SendHaptic(0f);
    }
}

