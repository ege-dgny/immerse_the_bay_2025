using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Android bridge for ESP32 Flex Glove BLE communication
/// Follows the same pattern as AfferenceAndroidBridge
/// </summary>
public sealed class FlexGloveAndroidBridge
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private const string WRAPPER_CLS = "com.flexglove.blewrapper.FlexGloveBLEWrapper";

    // Listener interface
    private const string IF_OPEN = "com.flexglove.blewrapper.FlexGloveBLEWrapper$OpenListener";
    private const string IF_RX = "com.flexglove.blewrapper.FlexGloveBLEWrapper$RxListener";
    private const string IF_SCAN_RESULT = "com.flexglove.blewrapper.FlexGloveBLEWrapper$ScanResultListener";

    private AndroidJavaObject _wrapper;
    private long _handle;
    private byte[] _rxBuffer = Array.Empty<byte>();
    private object _rxLock = new object();
#endif

    public bool IsOpen
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        get
        {
            if (_wrapper == null) return false;
            try
            {
                return _wrapper.Call<bool>("isOpen");
            }
            catch
            {
                return false;
            }
        }
#else
        get => false;
#endif
    }

    public bool Init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_wrapper != null) return true;

            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");

            _wrapper = new AndroidJavaObject(WRAPPER_CLS, context);
            Debug.Log("[FlexGloveBridge] Init OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FlexGloveBridge] Init FAILED: {e}");
            return false;
        }
#else
        return false;
#endif
    }

    public bool OpenBlocking(string deviceName, int timeoutMs = 10000)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        HardCloseGatt();

        if (!Init()) return false;

        using var evt = new ManualResetEventSlim(false);
        long got = 0;
        string errorMsg = null;

        Debug.Log($"[FlexGloveBridge] openAsync deviceName='{deviceName}' (timeoutMs={timeoutMs})");
        
        var openProxy = new OpenProxy(IF_OPEN, 
            h => { 
                got = h; 
                Debug.Log($"[FlexGloveBridge] onOpen handle=0x{h:X}"); 
                evt.Set(); 
            },
            err => {
                errorMsg = err;
                Debug.LogError($"[FlexGloveBridge] onError: {err}");
                evt.Set();
            });

        // Set up RX listener
        var rxProxy = new RxProxy(IF_RX, data =>
        {
            lock (_rxLock)
            {
                if (data != null && data.Length > 0)
                {
                    // string text = System.Text.Encoding.ASCII.GetString(data);
                    // Debug.Log($"[FlexGloveBridge] RxProxy received {data.Length} bytes: {text}"); // COMMENTED: Too noisy - receiving data constantly
                    // Append to buffer
                    int oldLen = _rxBuffer.Length;
                    Array.Resize(ref _rxBuffer, oldLen + data.Length);
                    Buffer.BlockCopy(data, 0, _rxBuffer, oldLen, data.Length);
                }
            }
        });

        try 
        { 
            _wrapper.Call("setRxListener", rxProxy);
            _wrapper.Call("openAsync", deviceName, timeoutMs, openProxy); 
        }
        catch (Exception e) 
        { 
            Debug.LogError($"[FlexGloveBridge] openAsync threw: {e}"); 
            return false; 
        }

        bool signaled = evt.Wait(timeoutMs > 0 ? timeoutMs : System.Threading.Timeout.Infinite);

        if (!signaled)
        {
            Debug.LogError("[FlexGloveBridge] openAsync timed out waiting for onOpen");
            return false;
        }

        if (!string.IsNullOrEmpty(errorMsg))
        {
            Debug.LogError($"[FlexGloveBridge] Connection failed: {errorMsg}");
            return false;
        }

        if (got == 0) 
        { 
            Debug.LogError("[FlexGloveBridge] openAsync returned handle=0"); 
            return false; 
        }
        
        _handle = got;

        Debug.Log("[FlexGloveBridge] OPEN OK");
        return true;
#else
        return false;
#endif
    }

    public void CloseBlocking()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_wrapper != null && _handle != 0)
            {
                try { _wrapper.Call("close", _handle); } catch { /* ignore */ }
                Debug.Log("[FlexGloveBridge] CLOSE done");
            }
        }
        finally
        {
            _handle = 0;
            lock (_rxLock)
            {
                _rxBuffer = Array.Empty<byte>();
            }
            try { HardCloseGatt(); } catch { }
        }
#endif
    }

    public byte[] RxBlocking(int timeoutMs)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_wrapper == null || _handle == 0) return Array.Empty<byte>();

        AndroidJNI.AttachCurrentThread();

        // Check buffer first
        lock (_rxLock)
        {
            if (_rxBuffer.Length > 0)
            {
                byte[] result = _rxBuffer;
                _rxBuffer = Array.Empty<byte>();
                return result;
            }
        }

        // Wait a bit for data (non-blocking check)
        System.Threading.Thread.Sleep(Math.Min(timeoutMs, 100));

        // Check buffer again
        lock (_rxLock)
        {
            if (_rxBuffer.Length > 0)
            {
                byte[] result = _rxBuffer;
                _rxBuffer = Array.Empty<byte>();
                return result;
            }
        }

        return Array.Empty<byte>();
#else
        return Array.Empty<byte>();
#endif
    }

    /// <summary>
    /// Instance-based hard close - only closes THIS bridge's GATT connection
    /// This allows multiple BLE devices to be connected concurrently
    /// </summary>
    public void HardCloseGatt()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJNI.AttachCurrentThread();

            if (_wrapper != null)
            {
                _wrapper.Call("hardCloseGatt");
                Debug.Log("[FlexGloveBridge] HardCloseGatt: closed this instance's GATT.");
            }
            else
            {
                Debug.Log("[FlexGloveBridge] HardCloseGatt: no wrapper instance.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FlexGloveBridge] HardCloseGatt exception: {e}");
        }
#endif
    }

    /// <summary>
    /// Scan for BLE devices and return list of device names
    /// </summary>
    /// <param name="scanDurationMs">How long to scan in milliseconds</param>
    /// <param name="onResult">Callback with array of device names</param>
    /// <param name="onError">Callback if scan fails</param>
    public void ScanForDevices(int scanDurationMs, Action<string[]> onResult, Action<string> onError)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Init())
        {
            onError?.Invoke("Failed to initialize bridge");
            return;
        }

        try
        {
            var scanProxy = new ScanResultProxy(IF_SCAN_RESULT, onResult, onError);
            _wrapper.Call("scanForDevices", scanDurationMs, scanProxy);
            Debug.Log($"[FlexGloveBridge] Started scanning for {scanDurationMs}ms");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FlexGloveBridge] ScanForDevices threw: {e}");
            onError?.Invoke($"Scan failed: {e.Message}");
        }
#else
        onError?.Invoke("BLE scanning only available on Android");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Listener proxies
    private sealed class OpenProxy : AndroidJavaProxy
    {
        private readonly Action<long> _onOpen;
        private readonly Action<string> _onError;

        public OpenProxy(string iface, Action<long> onOpen, Action<string> onError) : base(iface)
        {
            _onOpen = onOpen;
            _onError = onError;
        }

        public void onOpen(long h) => _onOpen?.Invoke(h);
        public void onError(string err) => _onError?.Invoke(err);
    }

    private sealed class RxProxy : AndroidJavaProxy
    {
        private readonly Action<byte[]> _onRx;

        public RxProxy(string iface, Action<byte[]> onRx) : base(iface)
        {
            _onRx = onRx;
        }

        public void onRx(byte[] data) => _onRx?.Invoke(data);
    }

    private sealed class ScanResultProxy : AndroidJavaProxy
    {
        private readonly Action<string[]> _onResult;
        private readonly Action<string> _onError;

        public ScanResultProxy(string iface, Action<string[]> onResult, Action<string> onError) : base(iface)
        {
            _onResult = onResult;
            _onError = onError;
        }

        public void onScanResult(string[] deviceNames) => _onResult?.Invoke(deviceNames);
        public void onScanError(string error) => _onError?.Invoke(error);
    }
#endif
}

