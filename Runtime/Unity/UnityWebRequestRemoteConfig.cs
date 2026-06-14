using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsPlatform;
using UnityEngine.Networking;

namespace AnalyticsPlatform.Unity
{

// WebGL-safe remote-config fetch (variants/experiments). Same GET as RemoteConfigClient (endpoint +
// query params), via UnityWebRequest so it works on WebGL where HttpClient cannot. Reuses
// RemoteConfigClient.Parse so the response mapping is identical. Without this, WebGL players never load
// assignments and every one falls to the "control" variant — the experiment's treatment arm goes empty.
public sealed class UnityWebRequestRemoteConfig : IRemoteConfigProvider
{
    private readonly AnalyticsConfig _config;

    public UnityWebRequestRemoteConfig(AnalyticsConfig config)
    {
        _config = config;
    }

    public Task<RemoteConfig> FetchAsync(CancellationToken cancellationToken)
    {
        var unitId = _config.PlayerId;
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return Task.FromResult(RemoteConfig.Empty);
        }

        var request = UnityWebRequest.Get(BuildUri(unitId!).AbsoluteUri);

        var completion = new TaskCompletionSource<RemoteConfig>(TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationTokenRegistration registration = default;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(static state => ((UnityWebRequest)state).Abort(), request);
        }

        var operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }

                var statusCode = (int)request.responseCode;
                var success = request.result == UnityWebRequest.Result.Success && statusCode is >= 200 and < 300;
                if (!success)
                {
                    var error = request.downloadHandler?.text;
                    if (string.IsNullOrEmpty(error))
                    {
                        error = request.error ?? "request failed";
                    }

                    completion.TrySetException(new InvalidOperationException(error));
                    return;
                }

                completion.TrySetResult(RemoteConfigClient.Parse(request.downloadHandler.text));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                registration.Dispose();
                request.Dispose();
            }
        };

        return completion.Task;
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
}
}
