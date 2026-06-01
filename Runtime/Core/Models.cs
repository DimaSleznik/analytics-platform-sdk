using System;
using System.Collections.Generic;

namespace AnalyticsPlatform
{

public sealed class AnalyticsEvent
{
    public AnalyticsEvent(
        string eventId,
        string eventName,
        int schemaVersion,
        string? userId,
        string? anonymousId,
        string sessionId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, object?> properties,
        IReadOnlyDictionary<string, object?> context)
    {
        EventId = eventId;
        EventName = eventName;
        SchemaVersion = schemaVersion;
        UserId = userId;
        AnonymousId = anonymousId;
        SessionId = sessionId;
        OccurredAt = occurredAt;
        Properties = properties;
        Context = context;
    }

    public string EventId { get; }
    public string EventName { get; }
    public int SchemaVersion { get; }
    public string? UserId { get; }
    public string? AnonymousId { get; }
    public string SessionId { get; }
    public DateTimeOffset OccurredAt { get; }
    public IReadOnlyDictionary<string, object?> Properties { get; }
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class AnalyticsBatch
{
    public AnalyticsBatch(IReadOnlyList<AnalyticsEvent> events)
    {
        Events = events;
    }

    public IReadOnlyList<AnalyticsEvent> Events { get; }
}

public sealed class TransportResult
{
    public TransportResult(bool success, int statusCode, string? error = null)
    {
        Success = success;
        StatusCode = statusCode;
        Error = error;
    }

    public bool Success { get; }
    public int StatusCode { get; }
    public string? Error { get; }
}

public sealed class FlushResult
{
    public FlushResult(int sent, int remaining, bool success, string? error = null)
    {
        Sent = sent;
        Remaining = remaining;
        Success = success;
        Error = error;
    }

    public int Sent { get; }
    public int Remaining { get; }
    public bool Success { get; }
    public string? Error { get; }
}
}
