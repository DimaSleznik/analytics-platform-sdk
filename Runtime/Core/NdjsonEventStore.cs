using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class NdjsonEventStore : IEventStore
{
    private readonly string _path;
    private readonly int _maxSize;

    public NdjsonEventStore(string path, int maxSize = 10_000)
    {
        _path = path;
        _maxSize = maxSize;
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public async Task AppendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        var events = (await ReadAllAsync(cancellationToken)).Where(item => item.EventId != analyticsEvent.EventId).ToList();
        events.Add(analyticsEvent);
        if (events.Count > _maxSize)
        {
            events = events.Skip(events.Count - _maxSize).ToList();
        }

        await WriteAllAsync(events, cancellationToken);
    }

    public async Task<IReadOnlyList<AnalyticsEvent>> PeekAsync(int count, CancellationToken cancellationToken)
    {
        return (await ReadAllAsync(cancellationToken)).Take(count).ToArray();
    }

    public async Task RemoveAsync(IReadOnlyCollection<string> eventIds, CancellationToken cancellationToken)
    {
        await WriteAllAsync((await ReadAllAsync(cancellationToken)).Where(item => !eventIds.Contains(item.EventId)), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return Task.CompletedTask;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return (await ReadAllAsync(cancellationToken)).Count;
    }

    private async Task<List<AnalyticsEvent>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new List<AnalyticsEvent>();
        }

        var lines = await File.ReadAllLinesAsync(_path, cancellationToken);
        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(DeserializeEvent)
            .Where(item => item is not null)
            .Cast<AnalyticsEvent>()
            .ToList();
    }

    private async Task WriteAllAsync(IEnumerable<AnalyticsEvent> events, CancellationToken cancellationToken)
    {
        var lines = events.Select(SerializeEvent);
        await File.WriteAllLinesAsync(_path, lines, cancellationToken);
    }

    private static string SerializeEvent(AnalyticsEvent item)
    {
        return StableJson.Stringify(new Dictionary<string, object?>
        {
            ["anonymousId"] = item.AnonymousId,
            ["context"] = item.Context,
            ["eventId"] = item.EventId,
            ["eventName"] = item.EventName,
            ["occurredAt"] = item.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["properties"] = item.Properties,
            ["schemaVersion"] = item.SchemaVersion,
            ["sessionId"] = item.SessionId,
            ["userId"] = item.UserId,
        });
    }

    private static AnalyticsEvent? DeserializeEvent(string line)
    {
        if (StableJson.Parse(line) is not Dictionary<string, object?> root)
        {
            return null;
        }

        var eventId = GetString(root, "eventId");
        var eventName = GetString(root, "eventName");
        var sessionId = GetString(root, "sessionId");
        var occurredAt = GetString(root, "occurredAt");
        if (eventId is null || eventName is null || sessionId is null || occurredAt is null)
        {
            return null;
        }

        return new AnalyticsEvent(
            eventId,
            eventName,
            GetInt(root, "schemaVersion", 1),
            GetString(root, "userId"),
            GetString(root, "anonymousId"),
            sessionId,
            DateTimeOffset.Parse(occurredAt, CultureInfo.InvariantCulture),
            GetMap(root, "properties"),
            GetMap(root, "context"));
    }

    private static string? GetString(Dictionary<string, object?> root, string key)
    {
        return root.TryGetValue(key, out var value) ? value as string : null;
    }

    private static int GetInt(Dictionary<string, object?> root, string key, int fallback)
    {
        return root.TryGetValue(key, out var value) && value is double number ? (int)number : fallback;
    }

    private static IReadOnlyDictionary<string, object?> GetMap(Dictionary<string, object?> root, string key)
    {
        return root.TryGetValue(key, out var value) && value is Dictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>();
    }
}
}
