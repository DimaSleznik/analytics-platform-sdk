using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class HttpAnalyticsTransport : ITransport
{
    private readonly AnalyticsConfig _config;
    private readonly HttpClient _client;
    private readonly IClock _clock;
    private readonly HmacSigner _signer;

    public HttpAnalyticsTransport(
        AnalyticsConfig config,
        HttpClient client,
        IClock clock,
        HmacSigner? signer = null)
    {
        _config = config;
        _client = client;
        _clock = clock;
        _signer = signer ?? new HmacSigner();
    }

    public async Task<TransportResult> SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
    {
        var payload = new
        {
            events = batch.Events.Select(ToPayload).ToArray(),
        };
        var json = StableJson.Stringify(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(EndpointBase(_config.Endpoint), "v1/ingest/batch"));
        request.Headers.TryAddWithoutValidation("x-project-id", _config.ProjectId);
        request.Headers.TryAddWithoutValidation("x-api-key", _config.ApiKey);
        if (_config.RequireHmac)
        {
            var timestamp = _clock.UtcNow.ToUnixTimeMilliseconds().ToString();
            request.Headers.TryAddWithoutValidation("x-timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("x-signature", _signer.Sign(_config.ApiKey, timestamp, payload));
        }

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        return new TransportResult(response.IsSuccessStatusCode, (int)response.StatusCode, response.IsSuccessStatusCode ? null : body);
    }

    private static object ToPayload(AnalyticsEvent item)
    {
        return new Dictionary<string, object?>
        {
            ["eventId"] = item.EventId,
            ["eventName"] = item.EventName,
            ["schemaVersion"] = item.SchemaVersion,
            ["userId"] = item.UserId ?? item.AnonymousId,
            ["sessionId"] = item.SessionId,
            ["occurredAt"] = item.OccurredAt.UtcDateTime.ToString("O"),
            ["properties"] = item.Properties,
            ["context"] = item.Context,
        };
    }

    private static Uri EndpointBase(Uri endpoint)
    {
        var uri = endpoint.AbsoluteUri;
        return uri.EndsWith("/", StringComparison.Ordinal) ? endpoint : new Uri($"{uri}/");
    }
}
}
