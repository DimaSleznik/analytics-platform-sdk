using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsPlatform;
using UnityEngine;

namespace AnalyticsPlatform.Unity
{

public sealed class UnityAnalyticsBehaviour : MonoBehaviour
{
    [SerializeField] private AnalyticsSettings settings = null!;
    [SerializeField] private bool startSessionOnAwake = true;

    private AnalyticsClient? _client;
    private CancellationTokenSource? _lifetime;
    private float _nextFlushAt;

    public IAnalytics? Client => _client;

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogError("AnalyticsSettings is required.");
            enabled = false;
            return;
        }

        var config = settings.ToConfig();
        config.PlayerId = ResolvePlayerId(settings.PlayerIdPrefsKey);
        var store = new NdjsonEventStore(
            System.IO.Path.Combine(Application.persistentDataPath, "analytics-queue.ndjson"),
            config.MaxQueueSize);
        var remoteConfig = new RemoteConfigClient(config, new HttpClient());
        var remoteConfigStore = new FileRemoteConfigStore(
            System.IO.Path.Combine(Application.persistentDataPath, "analytics-config.json"));
        var transport = new HttpAnalyticsTransport(config, new HttpClient(), new UnityClock());
        _client = AnalyticsClient.Create(
            config,
            store,
            transport,
            new UnityClock(),
            new GuidAnalyticsIdGenerator(),
            new UnityLogSink(),
            remoteConfigProvider: remoteConfig,
            remoteConfigStore: remoteConfigStore);
        _lifetime = new CancellationTokenSource();
        Analytics.Initialize(_client);
        _ = _client.RefreshConfigAsync(_lifetime.Token);
        if (startSessionOnAwake)
        {
            _client.StartSession();
        }

        _nextFlushAt = Time.unscaledTime + (float)config.FlushInterval.TotalSeconds;
    }

    private void Update()
    {
        if (_client == null || settings == null || Time.unscaledTime < _nextFlushAt)
        {
            return;
        }

        _nextFlushAt = Time.unscaledTime + (float)settings.ToConfig().FlushInterval.TotalSeconds;
        _ = FlushAsync();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            _client?.AppBackground();
            _ = FlushAsync();
            return;
        }

        _client?.AppForeground();
    }

    private void OnApplicationQuit()
    {
        if (_client != null)
        {
            _client.EndSession();
        }

        _ = FlushAsync();
    }

    private void OnDestroy()
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
    }

    private async Task FlushAsync()
    {
        if (_client == null || _lifetime == null)
        {
            return;
        }

        try
        {
            await _client.FlushAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string ResolvePlayerId(string key)
    {
        var value = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
        return value;
    }
}
}
