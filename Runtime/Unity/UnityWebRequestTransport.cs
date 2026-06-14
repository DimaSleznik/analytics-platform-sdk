using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsPlatform;
using UnityEngine.Networking;

namespace AnalyticsPlatform.Unity
{

// WebGL-safe event transport. UnityEngine.Networking.UnityWebRequest maps to the browser's fetch/XHR,
// unlike System.Net.Http.HttpClient (no sockets on WebGL). Produces the byte-identical request to
// HttpAnalyticsTransport (same endpoint/headers/body) by reusing StableJson + HmacSigner with the SAME
// payload object, so HMAC signatures match the backend. Single-threaded-safe: the request is created on
// the calling (main) thread and completion is bridged via op.completed -> TaskCompletionSource (no threads).
public sealed class UnityWebRequestTransport : ITransport
{
    private readonly AnalyticsConfig _config;
    private readonly IClock _clock;
    private readonly HmacSigner _signer;

    public UnityWebRequestTransport(
        AnalyticsConfig config,
        IClock clock,
        HmacSigner? signer = null)
    {
        _config = config;
        _clock = clock;
        _signer = signer ?? new HmacSigner();
    }

    public Task<TransportResult> SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
    {
        var payload = new
        {
            events = batch.Events.Select(ToPayload).ToArray(),
        };
        var json = StableJson.Stringify(payload);
        var url = new Uri(EndpointBase(_config.Endpoint), "v1/ingest/batch").AbsoluteUri;

        var body = Encoding.UTF8.GetBytes(json);
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json; charset=utf-8" },
            downloadHandler = new DownloadHandlerBuffer(),
        };
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        request.SetRequestHeader("x-project-id", _config.ProjectId);
        request.SetRequestHeader("x-api-key", _config.ApiKey);
        if (_config.RequireHmac)
        {
            var timestamp = _clock.UtcNow.ToUnixTimeMilliseconds().ToString();
            request.SetRequestHeader("x-timestamp", timestamp);
            request.SetRequestHeader("x-signature", _signer.Sign(_config.ApiKey, timestamp, payload));
        }

        var completion = new TaskCompletionSource<TransportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

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

                completion.TrySetResult(BuildResult(request));
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

    private static TransportResult BuildResult(UnityWebRequest request)
    {
        var statusCode = (int)request.responseCode;
        var success = request.result == UnityWebRequest.Result.Success && statusCode is >= 200 and < 300;
        if (success)
        {
            return new TransportResult(true, statusCode, null);
        }

        var error = request.downloadHandler?.text;
        if (string.IsNullOrEmpty(error))
        {
            error = request.error ?? "request failed";
        }

        return new TransportResult(false, statusCode, error);
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
