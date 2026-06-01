using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public interface IAnalytics
{
    void Identify(string userId);
    void SetUserProperty(string key, object? value);
    void StartSession();
    void EndSession();
    void AppForeground();
    void AppBackground();
    ExperimentVariant GetVariant(string experimentKey);
    T GetParam<T>(string experimentKey, string key, T fallback);
    bool Track(string name, IReadOnlyDictionary<string, object?>? properties = null);
    void SetConsent(bool granted);
    Task RefreshConfigAsync(CancellationToken cancellationToken = default);
    Task<FlushResult> FlushAsync(CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAnalyticsIdGenerator
{
    string NewId();
}

public interface IEventStore
{
    Task AppendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnalyticsEvent>> PeekAsync(int count, CancellationToken cancellationToken);
    Task RemoveAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
}

public interface ITransport
{
    Task<TransportResult> SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken);
}

public interface ILogSink
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public interface IRetryDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IRemoteConfigProvider
{
    Task<RemoteConfig> FetchAsync(CancellationToken cancellationToken);
}

public interface IRemoteConfigStore
{
    Task SaveAsync(RemoteConfig config, CancellationToken cancellationToken);
    Task<RemoteConfig?> LoadAsync(CancellationToken cancellationToken);
}
}
