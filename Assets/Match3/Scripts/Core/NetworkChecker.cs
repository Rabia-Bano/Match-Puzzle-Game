// ============================================================
//  NetworkChecker.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: FirebaseManagers GameObject (PreloaderScene), same
//             object jahan FirebaseInitializer / AuthManager /
//             ProfileManager / CloudSyncManager attached hain.
//  Call: koi Initialize() nahi chahiye — Awake() se hi kaam start
//        ho jata hai. Bas Instance is ready as soon as scene loads.
//  Access: NetworkChecker.Instance.IsOnline               (cached, instant)
//          await NetworkChecker.Instance.CheckConnectivityAsync()  (fresh-ish, cache-aware)
// ============================================================

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkChecker : MonoBehaviour
{
    public static NetworkChecker Instance { get; private set; }

    [Header("Config")]
    [Tooltip("Lightweight endpoint jo sirf HEAD request se 204/200 return kare. " +
             "Firebase project se related URL bhi use kar sakte ho, lekin generate_204 " +
             "style endpoint sabse tez aur data-light hota hai.")]
    [SerializeField] private string pingUrl = "https://www.gstatic.com/generate_204";

    [SerializeField] private float cacheDurationSeconds = 30f;
    [SerializeField] private int   timeoutSeconds        = 5;

    /// <summary>Fires jab bhi connectivity status TRUE se FALSE ya FALSE se TRUE badalta hai.</summary>
    public event Action<bool> OnConnectivityChanged;

    private bool  _cachedIsOnline = true;   // optimistic default — pehla check hone tak
    private float _lastCheckRealtime = -999f;
    private bool  _checkInFlight = false;

    /// <summary>Instant, non-blocking read of the last-known connectivity status.
    /// Cache 30s (configurable) ke liye valid rehta hai; background mein
    /// periodically refresh hoti rehti hai (Start() ka InvokeRepeating dekho).</summary>
    public bool IsOnline => _cachedIsOnline;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Turant ek check kardo taake game shuru hote hi accurate status mile,
        // phir har cacheDurationSeconds par background refresh chalta rahe.
        _ = CheckConnectivityAsync(forceRefresh: true);
        InvokeRepeating(nameof(BackgroundRefreshTick), cacheDurationSeconds, cacheDurationSeconds);
    }

    private void BackgroundRefreshTick()
    {
        _ = CheckConnectivityAsync(forceRefresh: true);
    }

    /// <summary>
    /// Cache 30s se purana ho ya forceRefresh=true ho to actual network ping karta hai,
    /// warna cached value turant (bina network call ke) return kar deta hai.
    /// Safe to call from anywhere with `await`.
    /// </summary>
    public Task<bool> CheckConnectivityAsync(bool forceRefresh = false)
    {
        bool cacheValid = (Time.realtimeSinceStartup - _lastCheckRealtime) < cacheDurationSeconds;
        if (!forceRefresh && cacheValid)
            return Task.FromResult(_cachedIsOnline);

        if (_checkInFlight)
        {
            // Ek check pehle se chal rahi hai — usi cached value ke saath turant return kardo
            // taake ek hi waqt mein multiple overlapping pings na chalein.
            return Task.FromResult(_cachedIsOnline);
        }

        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(PingCoroutine(tcs));
        return tcs.Task;
    }

    private IEnumerator PingCoroutine(TaskCompletionSource<bool> tcs)
    {
        _checkInFlight = true;
        bool result;

        // Pehle Unity ka built-in reachability check — Airplane mode / no radio
        // jaise cases mein bina network call ke hi turant "offline" pata chal jata hai.
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            result = false;
        }
        else
        {
            using (UnityWebRequest req = UnityWebRequest.Head(pingUrl))
            {
                req.timeout = timeoutSeconds;
                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                result = req.result == UnityWebRequest.Result.Success;
#else
                result = !req.isNetworkError && !req.isHttpError;
#endif
            }
        }

        UpdateCache(result);
        _checkInFlight = false;
        tcs.TrySetResult(result);
    }

    private void UpdateCache(bool isOnline)
    {
        _lastCheckRealtime = Time.realtimeSinceStartup;
        bool changed = _cachedIsOnline != isOnline;
        _cachedIsOnline = isOnline;

        if (changed)
        {
            Debug.Log($"[NetworkChecker] Connectivity changed -> {(isOnline ? "ONLINE" : "OFFLINE")}");
            OnConnectivityChanged?.Invoke(isOnline);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) CancelInvoke(nameof(BackgroundRefreshTick));
    }
}
