# Agent Native Analytics SDK

Reusable UPM package for sending analytics events to the Analytics Platform.

## Install

Unity Package Manager → Add package from git URL:

```
https://github.com/DimaSleznik/analytics-platform-sdk.git
```

Or pin it in `Packages/manifest.json`:

```json
"com.knightfantasy.analytics": "https://github.com/DimaSleznik/analytics-platform-sdk.git#v0.1.0"
```

## Quickstart

Create an `AnalyticsSettings` asset, set endpoint, project id and the API key environment variable name, then add `UnityAnalyticsBehaviour` to a bootstrap scene. The API key is read at runtime from that environment variable and should be injected by build or launch secrets.

The core can also run headless:

```csharp
var analytics = AnalyticsClient.Create(config, store, transport, clock, ids, log);
analytics.StartSession();
analytics.Track("session_start", new Dictionary<string, object?>
{
    ["build"] = "1.0.0",
    ["platform"] = "pc",
});
await analytics.FlushAsync(CancellationToken.None);
```

## Experiments

Call `RefreshConfigAsync` once after startup, then read variants at the point where the player actually sees the changed behavior. The first read logs one `experiment_exposure` event per session.

```csharp
await analytics.RefreshConfigAsync(CancellationToken.None);
var variant = analytics.GetVariant("starter_offer");
var starterGold = analytics.GetParam("starter_offer", "starterGold", 100);
```

## Environment

| Setting | Meaning |
|---|---|
| endpoint | Backend base URL, for example `http://localhost:3000/api` |
| projectId | Project id from the analytics backend |
| playerIdPrefsKey | Unity PlayerPrefs key for the anonymous sticky experiment id |
| apiKeyEnvironmentVariable | Environment variable that contains the project API key |
| requireHmac | Sends `x-signature` and `x-timestamp` |
| batchSize | Max events per flush |
| flushIntervalSeconds | Unity auto flush cadence |
| maxQueueSize | Durable queue cap |
| retryCount / retryBaseDelaySeconds / retryMaxDelaySeconds | Exponential backoff for transient flush failures |
| sessionTimeoutMinutes | Starts a new session after the app returns from the background past this timeout |

API keys are not serialized into settings assets.
