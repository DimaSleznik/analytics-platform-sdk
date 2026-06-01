using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class InMemoryEventStore : IEventStore
{
    private readonly List<AnalyticsEvent> _events = new();
    private readonly int _maxSize;

    public InMemoryEventStore(int maxSize = 10_000)
    {
        _maxSize = maxSize;
    }

    public Task AppendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        _events.RemoveAll(item => item.EventId == analyticsEvent.EventId);
        _events.Add(analyticsEvent);
        if (_events.Count > _maxSize)
        {
            _events.RemoveRange(0, _events.Count - _maxSize);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AnalyticsEvent>> PeekAsync(int count, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<AnalyticsEvent>>(_events.Take(count).ToArray());
    }

    public Task RemoveAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken)
    {
        _events.RemoveAll(item => eventIds.Contains(item.EventId));
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _events.Clear();
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_events.Count);
    }
}
}
