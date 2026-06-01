using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace AnalyticsPlatform.Tests
{

public sealed class AnalyticsCoreTests
{
    [Fact]
    public void StableJsonOrdersObjectKeys()
    {
        StableJson.Stringify(new Dictionary<string, object?>
        {
            ["z"] = 2,
            ["a"] = new Dictionary<string, object?>
            {
                ["b"] = true,
                ["a"] = "text",
            },
        }).Should().Be("{\"a\":{\"a\":\"text\",\"b\":true},\"z\":2}");
    }

    [Fact]
    public void HmacSignerMatchesBackendContract()
    {
        var payload = new
        {
            events = new[]
            {
                new Dictionary<string, object?>
                {
                    ["eventId"] = "evt-1",
                    ["eventName"] = "session_start",
                },
            },
        };

        new HmacSigner()
            .Sign("secret", "1700000000000", payload)
            .Should()
            .Be("e2bdaf0306cc39aa5ec73d2bec5c6e1402f6dcd4901c6ef6998e5730626e61e9");
    }

    [Fact]
    public void HmacSignerMatchesUnicodeAndFloatVector()
    {
        var payload = new Dictionary<string, object?>
        {
            ["unicode"] = "меч",
            ["float"] = 12.5,
            ["nested"] = new Dictionary<string, object?>
            {
                ["z"] = false,
                ["a"] = new object?[] { 1, "два" },
            },
        };

        StableJson.Stringify(payload).Should().Be("{\"float\":12.5,\"nested\":{\"a\":[1,\"два\"],\"z\":false},\"unicode\":\"меч\"}");
        new HmacSigner()
            .Sign("secret", "1780052348000", payload)
            .Should()
            .Be("244ec42aff84c27e80bdb8b4530a79d022de13739bb69dfaaffb9a2a177563c3");
    }

    [Fact]
    public void AssignmentMatchesBackendVector()
    {
        Assignment.Bucket("player-1", "onboarding:v1").Should().Be(6266);
        Assignment.Bucket("player-42", "onboarding:v1").Should().Be(4386);
        Assignment.Bucket("user-а", "onboarding:v1").Should().Be(169);
    }

    [Fact]
    public void RemoteConfigParsesAssignments()
    {
        var config = RemoteConfigClient.Parse(
            "{\"assignments\":[{\"experimentKey\":\"onboarding\",\"variant\":\"variant_b\",\"params\":{\"starterGold\":150}}]}");

        config.Variants["onboarding"].Name.Should().Be("variant_b");
        config.Variants["onboarding"].Parameters["starterGold"].Should().Be(150d);
    }

    [Fact]
    public void ExponentialBackoffGrowsUntilCap()
    {
        ExponentialBackoff.Delay(0, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.Zero);
        ExponentialBackoff.Delay(1, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromMilliseconds(100));
        ExponentialBackoff.Delay(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromMilliseconds(400));
        ExponentialBackoff.Delay(8, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ClientQueuesAndFlushesEvents()
    {
        var store = new InMemoryEventStore();
        var transport = new CapturingTransport();
        var client = AnalyticsClient.Create(Config(), store, transport, new FixedClock(), new IncrementingIds());

        client.Identify("player-1");
        client.Track("session_start", new Dictionary<string, object?> { ["build"] = "1" }).Should().BeTrue();
        var result = await client.FlushAsync();

        result.Success.Should().BeTrue();
        result.Sent.Should().Be(1);
        (await store.CountAsync(CancellationToken.None)).Should().Be(0);
        transport.Batch.Events[0].UserId.Should().Be("player-1");
    }

    [Fact]
    public async Task GetVariantLogsExposureOnce()
    {
        var store = new InMemoryEventStore();
        var client = AnalyticsClient.Create(
            Config(playerId: "player-1"),
            store,
            new CapturingTransport(),
            new FixedClock(),
            new IncrementingIds(),
            remoteConfigProvider: new FixedRemoteConfigProvider(new RemoteConfig(
                new Dictionary<string, ExperimentVariant>
                {
                    ["onboarding"] = new(
                        "variant_b",
                        new Dictionary<string, object?> { ["starterGold"] = 150 }),
                })));

        await client.RefreshConfigAsync();
        client.GetVariant("onboarding").Name.Should().Be("variant_b");
        client.GetParam("onboarding", "starterGold", 0).Should().Be(150);
        client.GetVariant("onboarding").Name.Should().Be("variant_b");

        var events = await store.PeekAsync(10, CancellationToken.None);
        events.Should().ContainSingle(item => item.EventName == "experiment_exposure");
        events.Single(item => item.EventName == "experiment_exposure")
            .Properties["variant"]
            .Should()
            .Be("variant_b");
    }

    [Fact]
    public async Task RefreshConfigFallsBackToCache()
    {
        var store = new MemoryRemoteConfigStore(new RemoteConfig(
            new Dictionary<string, ExperimentVariant>
            {
                ["offer"] = new("control", new Dictionary<string, object?>()),
            }));
        var client = AnalyticsClient.Create(
            Config(playerId: "player-1"),
            new InMemoryEventStore(),
            new CapturingTransport(),
            new FixedClock(),
            new IncrementingIds(),
            remoteConfigProvider: new FailingRemoteConfigProvider(),
            remoteConfigStore: store);

        await client.RefreshConfigAsync();

        client.GetVariant("offer").Name.Should().Be("control");
    }

    [Fact]
    public async Task FlushRetriesTransientFailuresWithBackoff()
    {
        var store = new InMemoryEventStore();
        var transport = new SequenceTransport(
            new TransportResult(false, 500, "down"),
            new TransportResult(false, 429, "slow"),
            new TransportResult(true, 202));
        var delay = new CapturingRetryDelay();
        var config = Config();
        config.RetryCount = 2;
        config.RetryBaseDelay = TimeSpan.FromMilliseconds(10);
        config.RetryMaxDelay = TimeSpan.FromMilliseconds(30);
        var client = AnalyticsClient.Create(config, store, transport, new FixedClock(), new IncrementingIds(), retryDelay: delay);

        client.Track("session_start").Should().BeTrue();
        var result = await client.FlushAsync();

        result.Success.Should().BeTrue();
        result.Sent.Should().Be(1);
        transport.Attempts.Should().Be(3);
        delay.Delays.Should().Equal(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task HttpTransportPreservesApiPathWithoutTrailingSlash()
    {
        var config = Config();
        config.Endpoint = new Uri("http://localhost:3000/api");
        var handler = new CapturingHttpHandler();
        using var http = new HttpClient(handler);
        var transport = new HttpAnalyticsTransport(config, http, new FixedClock());
        var batch = new AnalyticsBatch(new[]
        {
            new AnalyticsEvent(
                "event-1",
                "session_start",
                1,
                "user",
                null,
                "session",
                DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
                new Dictionary<string, object?>(),
                new Dictionary<string, object?>()),
        });

        var result = await transport.SendAsync(batch, CancellationToken.None);

        result.Success.Should().BeTrue();
        handler.RequestUri.Should().Be("http://localhost:3000/api/v1/ingest/batch");
    }

    [Fact]
    public async Task HttpTransportPreservesApiPathWithTrailingSlash()
    {
        var config = Config();
        var handler = new CapturingHttpHandler();
        using var http = new HttpClient(handler);
        var transport = new HttpAnalyticsTransport(config, http, new FixedClock());
        var batch = new AnalyticsBatch(new[]
        {
            new AnalyticsEvent(
                "event-1",
                "session_start",
                1,
                "user",
                null,
                "session",
                DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
                new Dictionary<string, object?>(),
                new Dictionary<string, object?>()),
        });

        var result = await transport.SendAsync(batch, CancellationToken.None);

        result.Success.Should().BeTrue();
        handler.RequestUri.Should().Be("http://localhost:3000/api/v1/ingest/batch");
    }

    [Fact]
    public async Task AppForegroundStartsNewSessionAfterTimeout()
    {
        var store = new InMemoryEventStore();
        var clock = new MutableClock(DateTimeOffset.Parse("2026-05-30T00:00:00Z"));
        var client = AnalyticsClient.Create(Config(), store, new CapturingTransport(), clock, new IncrementingIds());

        client.StartSession();
        client.AppBackground();
        clock.UtcNow = clock.UtcNow.AddMinutes(31);
        client.AppForeground();

        var events = await store.PeekAsync(10, CancellationToken.None);
        events.Select(item => item.EventName).Should().Equal(
            "session_start",
            "app_background",
            "session_start",
            "app_foreground");
        events[0].SessionId.Should().NotBe(events[2].SessionId);
    }

    [Fact]
    public async Task ConsentOptOutClearsQueueAndBlocksTracking()
    {
        var store = new InMemoryEventStore();
        var client = AnalyticsClient.Create(Config(consentRequired: true), store, new CapturingTransport(), new FixedClock(), new IncrementingIds());

        client.Track("session_start").Should().BeFalse();
        client.SetConsent(true);
        client.Track("session_start").Should().BeTrue();
        client.SetConsent(false);

        (await store.CountAsync(CancellationToken.None)).Should().Be(0);
        client.Track("session_start").Should().BeFalse();
    }

    [Fact]
    public async Task NdjsonStoreSurvivesRecreationAndDeduplicatesIds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ndjson");
        var first = new NdjsonEventStore(path);
        var analyticsEvent = new AnalyticsEvent(
            "evt-1",
            "session_start",
            1,
            "user",
            null,
            "session",
            DateTimeOffset.Parse("2026-05-30T00:00:00Z"),
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>());

        await first.AppendAsync(analyticsEvent, CancellationToken.None);
        await first.AppendAsync(analyticsEvent, CancellationToken.None);

        var second = new NdjsonEventStore(path);
        (await second.PeekAsync(10, CancellationToken.None)).Should().ContainSingle();
        File.Delete(path);
    }

    private static AnalyticsConfig Config(
        bool consentRequired = false,
        string? playerId = null)
    {
        return new AnalyticsConfig
        {
            Endpoint = new Uri("http://localhost:3000/api/"),
            ProjectId = "project",
            ApiKey = "key",
            PlayerId = playerId,
            ConsentRequired = consentRequired,
        };
    }
}

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-05-30T00:00:00Z");
}

public sealed class MutableClock : IClock
{
    public MutableClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }
}

public sealed class IncrementingIds : IAnalyticsIdGenerator
{
    private int _value;

    public string NewId()
    {
        _value += 1;
        return $"id-{_value}";
    }
}

public sealed class CapturingTransport : ITransport
{
    public AnalyticsBatch Batch { get; private set; } = new(Array.Empty<AnalyticsEvent>());

    public Task<TransportResult> SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
    {
        Batch = batch;
        return Task.FromResult(new TransportResult(true, 202));
    }
}

public sealed class SequenceTransport : ITransport
{
    private readonly Queue<TransportResult> _results;

    public SequenceTransport(params TransportResult[] results)
    {
        _results = new Queue<TransportResult>(results);
    }

    public int Attempts { get; private set; }

    public Task<TransportResult> SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
    {
        Attempts += 1;
        return Task.FromResult(_results.Dequeue());
    }
}

public sealed class CapturingRetryDelay : IRetryDelay
{
    public List<TimeSpan> Delays { get; } = new();

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}

public sealed class CapturingHttpHandler : HttpMessageHandler
{
    public string? RequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri?.ToString();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}

public sealed class FixedRemoteConfigProvider : IRemoteConfigProvider
{
    private readonly RemoteConfig _config;

    public FixedRemoteConfigProvider(RemoteConfig config)
    {
        _config = config;
    }

    public Task<RemoteConfig> FetchAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_config);
    }
}

public sealed class FailingRemoteConfigProvider : IRemoteConfigProvider
{
    public Task<RemoteConfig> FetchAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("offline");
    }
}

public sealed class MemoryRemoteConfigStore : IRemoteConfigStore
{
    private RemoteConfig? _config;

    public MemoryRemoteConfigStore(RemoteConfig? config = null)
    {
        _config = config;
    }

    public Task SaveAsync(RemoteConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        return Task.CompletedTask;
    }

    public Task<RemoteConfig?> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_config);
    }
}
}
