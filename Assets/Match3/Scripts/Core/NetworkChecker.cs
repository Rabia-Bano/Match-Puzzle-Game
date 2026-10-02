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

    public event Action<bool> OnConnectivityChanged;

    private bool  _cachedIsOnline = true;
    private float _lastCheckRealtime = -999f;
    private bool  _checkInFlight = false;

    public bool IsOnline => _cachedIsOnline;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _ = CheckConnectivityAsync(forceRefresh: true);
        InvokeRepeating(nameof(BackgroundRefreshTick), cacheDurationSeconds, cacheDurationSeconds);
    }

    private void BackgroundRefreshTick()
    {
        _ = CheckConnectivityAsync(forceRefresh: true);
    }

    public Task<bool> CheckConnectivityAsync(bool forceRefresh = false)
    {
        bool cacheValid = (Time.realtimeSinceStartup - _lastCheckRealtime) < cacheDurationSeconds;
        if (!forceRefresh && cacheValid)
            return Task.FromResult(_cachedIsOnline);

        if (_checkInFlight)
        {
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
