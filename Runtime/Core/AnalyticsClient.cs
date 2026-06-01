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
    private readonly Dictionary<string, object?> _userProperties = new();
    private string? _userId;
    private string _anonymousId;
    private string _sessionId;
    private DateTimeOffset? _lastBackgroundAt;
    private bool _consentGranted;

    private AnalyticsClient(
        AnalyticsConfig config,
        IEventStore store,
        ITransport transport,
        IClock clock,
        IAnalyticsIdGenerator ids,
        ILogSink log,
        IRetryDelay retryDelay)
    {
        _config = config;
        _store = store;
        _transport = transport;
        _clock = clock;
        _ids = ids;
        _log = log;
        _retryDelay = retryDelay;
        _anonymousId = ids.NewId();
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
        IRetryDelay? retryDelay = null)
    {
        return new AnalyticsClient(
            config,
            store,
            transport,
            clock,
            ids,
            log ?? new NullLogSink(),
            retryDelay ?? new SystemRetryDelay());
    }

    public void Identify(string userId)
    {
        _userId = string.IsNullOrWhiteSpace(userId) ? null : userId;
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
        return context;
    }

    private static bool ShouldRetry(TransportResult result)
    {
        return result.StatusCode == 0 || result.StatusCode == 429 || result.StatusCode >= 500;
    }
}
}
