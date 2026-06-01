using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyticsPlatform
{

public sealed class RemoteConfigClient : IRemoteConfigProvider
{
    private readonly AnalyticsConfig _config;
    private readonly HttpClient _client;

    public RemoteConfigClient(AnalyticsConfig config, HttpClient client)
    {
        _config = config;
        _client = client;
    }

    public async Task<RemoteConfig> FetchAsync(CancellationToken cancellationToken)
    {
        var unitId = _config.PlayerId;
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return RemoteConfig.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(unitId));
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        }

        return Parse(await response.Content.ReadAsStringAsync());
    }

    public static RemoteConfig Parse(string json)
    {
        if (StableJson.Parse(json) is not Dictionary<string, object?> root ||
            root.TryGetValue("assignments", out var assignmentsValue) is false ||
            assignmentsValue is not List<object?> assignments)
        {
            return RemoteConfig.Empty;
        }

        var variants = new Dictionary<string, ExperimentVariant>(StringComparer.Ordinal);
        foreach (var item in assignments)
        {
            if (item is not Dictionary<string, object?> assignment)
            {
                continue;
            }

            var experimentKey = GetString(assignment, "experimentKey");
            var variant = GetString(assignment, "variant");
            if (experimentKey is null || variant is null)
            {
                continue;
            }

            variants[experimentKey] = new ExperimentVariant(
                variant,
                GetMap(assignment, "params"));
        }

        return new RemoteConfig(variants);
    }

    private Uri BuildUri(string playerId)
    {
        var endpoint = EndpointBase(_config.Endpoint);
        var query = new List<string>
        {
            $"project_id={Uri.EscapeDataString(_config.ProjectId)}",
            $"player_id={Uri.EscapeDataString(playerId)}",
        };
        Add(query, "platform", _config.Platform);
        Add(query, "version", _config.Version);
        Add(query, "cohort", _config.Cohort);
        return new Uri(endpoint, $"v1/config?{string.Join("&", query)}");
    }

    private static void Add(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{key}={Uri.EscapeDataString(value)}");
        }
    }

    private static Uri EndpointBase(Uri endpoint)
    {
        var uri = endpoint.AbsoluteUri;
        return uri.EndsWith("/", StringComparison.Ordinal) ? endpoint : new Uri($"{uri}/");
    }

    private static string? GetString(Dictionary<string, object?> root, string key)
    {
        return root.TryGetValue(key, out var value) ? value as string : null;
    }

    private static IReadOnlyDictionary<string, object?> GetMap(Dictionary<string, object?> root, string key)
    {
        return root.TryGetValue(key, out var value) && value is Dictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>();
    }
}
}
