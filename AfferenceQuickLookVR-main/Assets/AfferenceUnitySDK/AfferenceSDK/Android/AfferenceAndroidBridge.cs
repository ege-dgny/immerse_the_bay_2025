using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

public sealed class AfferenceAndroidBridge
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private const string WRAPPER_CLS = "com.afference.afferenceringwrapper.AfferenceRingWrapper";

    // Nested listener interfaces (exact FQCNs from your AAR)
    private const string IF_OPEN   = "com.afference.afferenceringwrapper.AfferenceRingWrapper$OpenListener";

    private AndroidJavaObject _wrapper;
    private long _handle;
#endif

    public bool IsOpen
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        get => _handle != 0;
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
            _wrapper = new AndroidJavaObject(WRAPPER_CLS);
            Debug.Log("[Bridge] Init OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bridge] Init FAILED: {e}");
            return false;
        }
#else
        return false;
#endif
    }

    public bool OpenBlocking(string path, int timeoutMs = 0)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // NOTE: Removed HardCloseGatt() call here to allow concurrent BLE connections
        // Each bridge instance manages its own GATT connection independently
        // Only close existing Afference ring connection if reopening the same instance
        
        if (!Init()) return false;

        using var evt = new ManualResetEventSlim(false);
        long got = 0;

        Debug.Log($"[Bridge] openAsync path='{path}' (waiting for onOpen, timeoutMs={timeoutMs})");
        var proxy = new OpenProxy(IF_OPEN, h => { got = h; Debug.Log($"[Bridge] onOpen handle=0x{h:X}"); evt.Set(); });

        try { _wrapper.Call("openAsync", path, proxy); }
        catch (Exception e) { Debug.LogError($"[Bridge] openAsync threw: {e}"); return false; }

        bool signaled = evt.Wait(timeoutMs > 0 ? timeoutMs : System.Threading.Timeout.Infinite);

        if (!signaled)
        {
            Debug.LogError("[Bridge] openAsync timed out waiting for onOpen");
            return false;
        }

        if (got == 0) { Debug.LogError("[Bridge] openAsync returned handle=0"); return false; }
        _handle = got;

        try { _wrapper.Call("updateConnectionSettings"); } catch { /*ignore */ }

        Debug.Log("[Bridge] OPEN OK");
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
            Debug.Log("[Bridge] CLOSE done");
        }
    }
    finally
    {
        _handle = 0;
        // NOTE: HardCloseGatt() includes a safety check to skip if FlexGlove is connected
        // This prevents any potential interference with FlexGlove's instance-based GATT connection
        // Even though they're different Java classes, we're being extra cautious
        try { HardCloseGatt(); } catch { }
    }
#endif
    }

    public int StatusBlocking(byte[] errBuf, int maxLen)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_wrapper == null || _handle == 0) return -1;

        int status = 0;

        using (var statusResult = _wrapper.Call<AndroidJavaObject>("unityStatus", _handle))
        {
            int n = 0;
            try { status = statusResult.Get<int>("status"); } catch { status = 0; }
            if (status < 0)
                { 
                    try { n = statusResult.Get<int>("length"); } catch { n = 0; }
                    if (n <= 0) { status = -1; return status; }

                    using (var bufObj = statusResult.Get<AndroidJavaObject>("resultBuffer"))
                    {
                        if (bufObj == null) { status = -1; return status; }

                        var raw = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(bufObj.GetRawObject());
                        if (raw == null || raw.Length == 0) return -1;
                        int copy = Math.Min(n, Math.Min(raw.Length, Math.Min(maxLen, errBuf.Length)));
                        if (copy > 0) Buffer.BlockCopy(raw, 0, errBuf, 0, copy);
                    }
                 }
         }
         return status;
#else
        return -1;
#endif
    }

    public int TxBlocking(byte[] data)
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (_wrapper == null || _handle == 0) return -1;
        if (data == null || data.Length == 0) return 0;

        AndroidJNI.AttachCurrentThread();

        int wrote = _wrapper.Call<int>("tx", _handle, data, data.Length);

        return (wrote >= 0) ? 0 : -4;
#else
        return -1;
#endif
    }

    public byte[] RxBlocking(int timeoutMs)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_wrapper == null || _handle == 0) return null;

        AndroidJNI.AttachCurrentThread(); 

        using (var rxResult = _wrapper.Call<AndroidJavaObject>("unityRx", _handle, timeoutMs))
        {
            if (rxResult == null) { return Array.Empty<byte>(); }

            int n = 0;
            try { n = rxResult.Get<int>("length"); } catch { n = 0; }
            if (n <= 0) { return Array.Empty<byte>(); }

            using (var bufObj = rxResult.Get<AndroidJavaObject>("resultBuffer"))
            {
                if (bufObj == null) { return Array.Empty<byte>(); }

                var raw = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(bufObj.GetRawObject());
                if (raw == null || raw.Length == 0) return Array.Empty<byte>();
                if (n > raw.Length) n = raw.Length;
                var result = new byte[n];
                Buffer.BlockCopy(raw, 0, result, 0, n);
                return result;
            }
         }
#else
        return Array.Empty<byte>();
#endif
    }

    /// <summary>
    /// Hard close Afference ring's GATT connection
    /// NOTE: This uses a static GATT reference from AfferenceRingWrapper (Java AAR)
    /// Safety check: Verifies FlexGlove is not connected before closing to prevent interference
    /// For true concurrent support, AfferenceRingWrapper Java code should be updated to use instance-based GATT
    /// </summary>
    public static void HardCloseGatt()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        // Safety check: Don't close if FlexGlove is connected (to prevent interference)
        // Even though they're different Java classes, we want to be extra safe
        var flexGloveWrapper = UnityEngine.Object.FindFirstObjectByType<FlexGloveBLEWrapper>();
        if (flexGloveWrapper != null && flexGloveWrapper.IsConnected)
        {
            Debug.LogWarning("[Bridge] HardCloseGatt: Skipping - FlexGlove is connected. This prevents potential interference.");
            return;
        }

        AndroidJNI.AttachCurrentThread();

        using (var cls = new AndroidJavaClass(WRAPPER_CLS))
        {
            var gatt = cls.GetStatic<AndroidJavaObject>("gatt");
            if (gatt == null)
            {
                Debug.Log("[Bridge] HardCloseGatt: no Afference ring GATT to close.");
                return;
            }

            Debug.Log("[Bridge] HardCloseGatt: closing Afference ring GATT only (FlexGlove uses separate instance)");
            try { gatt.Call("disconnect"); } catch { }
            try { gatt.Call("close"); } catch { }
            try { cls.SetStatic<AndroidJavaObject>("gatt", null); } catch { }

            Debug.Log("[Bridge] HardCloseGatt: Afference ring GATT closed.");
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[Bridge] HardCloseGatt exception: {e}");
    }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // ---------- Listener proxies  ----------
    private sealed class OpenProxy:AndroidJavaProxy
    {
        private readonly Action<long> _cb;
        public OpenProxy(string f, Action<long> cb):base(f){_cb=cb;}
        public void onOpen(long h)=>_cb?.Invoke(h);
    }
#endif
}
