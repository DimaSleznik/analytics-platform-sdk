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
        var store = new NdjsonEventStore(
            System.IO.Path.Combine(Application.persistentDataPath, "analytics-queue.ndjson"),
            config.MaxQueueSize);
        var transport = new HttpAnalyticsTransport(config, new HttpClient(), new UnityClock());
        _client = AnalyticsClient.Create(config, store, transport, new UnityClock(), new GuidAnalyticsIdGenerator(), new UnityLogSink());
        _lifetime = new CancellationTokenSource();
        Analytics.Initialize(_client);
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
}
}
