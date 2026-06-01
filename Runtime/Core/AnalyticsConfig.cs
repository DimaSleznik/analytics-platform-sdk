using System;
using System.Collections.Generic;

namespace AnalyticsPlatform
{

public sealed class AnalyticsConfig
{
    public Uri Endpoint { get; set; } = new("http://localhost:3000/api");
    public string ProjectId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? Platform { get; set; }
    public string? Version { get; set; }
    public string? Cohort { get; set; }
    public bool RequireHmac { get; set; } = true;
    public int BatchSize { get; set; } = 50;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int RetryCount { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxQueueSize { get; set; } = 10_000;
    public double SamplingRate { get; set; } = 1;
    public bool ConsentRequired { get; set; }
    public Dictionary<string, object?> DefaultContext { get; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException("ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required.");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("BatchSize must be positive.");
        }

        if (MaxQueueSize <= 0)
        {
            throw new InvalidOperationException("MaxQueueSize must be positive.");
        }

        if (RetryCount < 0)
        {
            throw new InvalidOperationException("RetryCount cannot be negative.");
        }

        if (SamplingRate is < 0 or > 1)
        {
            throw new InvalidOperationException("SamplingRate must be between 0 and 1.");
        }
    }
}
}
