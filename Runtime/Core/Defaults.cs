using System;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class GuidAnalyticsIdGenerator : IAnalyticsIdGenerator
{
    public string NewId() => Guid.NewGuid().ToString("N");
}

public sealed class NullLogSink : ILogSink
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
}

public sealed class SystemRetryDelay : IRetryDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
}
