using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class AnalyticsClient : IAnalytics
{
    private readonly AnalyticsConfig _config;
    private readonly IEventStore _store;
    private readonly ITransport _transport;
    private readonly IClock _clock;
    private readonly IAnalyticsIdGenerator _ids;
    private readonly ILogSink _log;
    private readonly IRetryDelay _retryDelay;
    private readonly IRemoteConfigProvider? _remoteConfigProvider;
    private readonly IRemoteConfigStore? _remoteConfigStore;
    private readonly Dictionary<string, object?> _userProperties = new();
    private readonly HashSet<string> _exposedExperiments = new(StringComparer.Ordinal);
    private string? _userId;
    private string _anonymousId;
    private string _sessionId;
    private DateTimeOffset? _lastBackgroundAt;
    private bool _consentGranted;
    private RemoteConfig _remoteConfig = RemoteConfig.Empty;

    private AnalyticsClient(
        AnalyticsConfig config,
        IEventStore store,
        ITransport transport,
        IClock clock,
        IAnalyticsIdGenerator ids,
        ILogSink log,
        IRetryDelay retryDelay,
        IRemoteConfigProvider? remoteConfigProvider,
        IRemoteConfigStore? remoteConfigStore)
    {
        _config = config;
        _store = store;
        _transport = transport;
        _clock = clock;
        _ids = ids;
        _log = log;
        _retryDelay = retryDelay;
        _remoteConfigProvider = remoteConfigProvider;
        _remoteConfigStore = remoteConfigStore;
        _anonymousId = string.IsNullOrWhiteSpace(config.PlayerId) ? ids.NewId() : config.PlayerId!;
        _sessionId = ids.NewId();
        _consentGranted = !config.ConsentRequired;
        _config.Validate();
    }

    public static AnalyticsClient Create(
        AnalyticsConfig config,
        IEventStore store,
        ITransport transport,
        IClock clock,
        IAnalyticsIdGenerator ids,
        ILogSink? log = null,
        IRetryDelay? retryDelay = null,
        IRemoteConfigProvider? remoteConfigProvider = null,
        IRemoteConfigStore? remoteConfigStore = null)
    {
        return new AnalyticsClient(
            config,
            store,
            transport,
            clock,
            ids,
            log ?? new NullLogSink(),
            retryDelay ?? new SystemRetryDelay(),
            remoteConfigProvider,
            remoteConfigStore);
    }

    public void Identify(string userId)
    {
        _userId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        if (_userId is not null)
        {
            _config.PlayerId = _userId;
        }
    }

    public void SetUserProperty(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _userProperties[key] = value;
    }

    public void StartSession()
    {
        _sessionId = _ids.NewId();
        Track("session_start");
    }

    public void EndSession()
    {
        Track("session_end");
    }

    public void AppForeground()
    {
        if (_lastBackgroundAt.HasValue && _clock.UtcNow - _lastBackgroundAt.Value >= _config.SessionTimeout)
        {
            StartSession();
        }

        _lastBackgroundAt = null;
        Track("app_foreground");
    }

    public void AppBackground()
    {
        _lastBackgroundAt = _clock.UtcNow;
        Track("app_background");
    }

    public ExperimentVariant GetVariant(string experimentKey)
    {
        if (string.IsNullOrWhiteSpace(experimentKey))
        {
            return new ExperimentVariant("control", new Dictionary<string, object?>());
        }

        if (!_remoteConfig.Variants.TryGetValue(experimentKey, out var variant))
        {
            return new ExperimentVariant("control", new Dictionary<string, object?>());
        }

        TrackExposure(experimentKey, variant.Name);
        return variant;
    }

    public T GetParam<T>(string experimentKey, string key, T fallback)
    {
        var variant = GetVariant(experimentKey);
        if (!variant.Parameters.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        try
        {
            if (value is T typed)
            {
                return typed;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public bool Track(string name, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (!_consentGranted || _config.SamplingRate <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var analyticsEvent = new AnalyticsEvent(
            _ids.NewId(),
            name,
            1,
            _userId,
            _anonymousId,
            _sessionId,
            _clock.UtcNow,
            properties ?? new Dictionary<string, object?>(),
            BuildContext());

        _store.AppendAsync(analyticsEvent, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }

    public void SetConsent(bool granted)
    {
        _consentGranted = granted;
        if (!granted)
        {
            _store.ClearAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public async Task RefreshConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_remoteConfigProvider is null)
        {
            _remoteConfig = await LoadCachedConfig(cancellationToken) ?? RemoteConfig.Empty;
            return;
        }

        try
        {
            _remoteConfig = await _remoteConfigProvider.FetchAsync(cancellationToken);
            if (_remoteConfigStore is not null)
            {
                await _remoteConfigStore.SaveAsync(_remoteConfig, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _log.Warning("Remote config fetch failed; using cached assignments.");
            _remoteConfig = await LoadCachedConfig(cancellationToken) ?? RemoteConfig.Empty;
        }
    }

    public async Task<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        var events = await _store.PeekAsync(_config.BatchSize, cancellationToken);
        if (events.Count == 0)
        {
            return new FlushResult(0, 0, true);
        }

        TransportResult? lastResult = null;
        Exception? lastException = null;
        var attempts = _config.RetryCount + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                lastResult = await _transport.SendAsync(new AnalyticsBatch(events), cancellationToken);
                if (lastResult.Success)
                {
                    var eventIds = new string[events.Count];
                    for (var i = 0; i < events.Count; i++)
                    {
                        eventIds[i] = events[i].EventId;
                    }

                    await _store.RemoveAsync(eventIds, cancellationToken);
                    return new FlushResult(events.Count, await _store.CountAsync(cancellationToken), true);
                }

                if (!ShouldRetry(lastResult) || attempt == attempts - 1)
                {
                    break;
                }

                _log.Warning($"Analytics flush failed with {lastResult.StatusCode}; retrying.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastException = exception;
                if (attempt == attempts - 1)
                {
                    break;
                }

                _log.Warning("Analytics flush failed; retrying.");
            }

            await _retryDelay.DelayAsync(
                ExponentialBackoff.Delay(attempt + 1, _config.RetryBaseDelay, _config.RetryMaxDelay),
                cancellationToken);
        }

        if (lastException is not null)
        {
            _log.Error("Analytics flush failed.", lastException);
            return new FlushResult(0, await _store.CountAsync(cancellationToken), false, lastException.Message);
        }

        return new FlushResult(
            0,
            await _store.CountAsync(cancellationToken),
            false,
            lastResult?.Error);
    }

    private Dictionary<string, object?> BuildContext()
    {
        var context = new Dictionary<string, object?>(_config.DefaultContext)
        {
            ["userProperties"] = new Dictionary<string, object?>(_userProperties),
        };
        AddContext(context, "platform", _config.Platform);
        AddContext(context, "version", _config.Version);
        AddContext(context, "app_version", _config.AppVersion ?? _config.Version);
        AddContext(context, "build_id", _config.BuildId);
        AddContext(context, "git_sha", _config.GitSha);
        AddContext(context, "content_version", _config.ContentVersion);
        AddContext(context, "sdk_version", _config.SdkVersion);
        return context;
    }

    private static void AddContext(IDictionary<string, object?> context, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            context[key] = value;
        }
    }

    private void TrackExposure(string experimentKey, string variant)
    {
        var exposureKey = $"{_sessionId}:{experimentKey}";
        if (!_exposedExperiments.Add(exposureKey))
        {
            return;
        }

        var unitId = _userId ?? _anonymousId;
        Track("experiment_exposure", new Dictionary<string, object?>
        {
            ["experiment_key"] = experimentKey,
            ["variant"] = variant,
            ["unit_id"] = unitId,
            ["exposed_at"] = _clock.UtcNow.UtcDateTime.ToString("O"),
        });
    }

    private Task<RemoteConfig?> LoadCachedConfig(CancellationToken cancellationToken)
    {
        return _remoteConfigStore?.LoadAsync(cancellationToken) ?? Task.FromResult<RemoteConfig?>(null);
    }

    private static bool ShouldRetry(TransportResult result)
    {
        return result.StatusCode == 0 || result.StatusCode == 429 || result.StatusCode >= 500;
    }
}
}
