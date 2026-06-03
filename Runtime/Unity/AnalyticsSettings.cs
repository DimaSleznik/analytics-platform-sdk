using System;
using AnalyticsPlatform;
using UnityEngine;

namespace AnalyticsPlatform.Unity
{

[CreateAssetMenu(menuName = "Analytics/Analytics Settings", fileName = "AnalyticsSettings")]
public sealed class AnalyticsSettings : ScriptableObject
{
    [SerializeField] private string endpoint = "http://localhost:3000/api";
    [SerializeField] private string projectId = "";
    [SerializeField] private string buildId = "";
    [SerializeField] private string contentVersion = "";
    [SerializeField] private string gitShaEnvironmentVariable = "KF_ANALYTICS_GIT_SHA";
    [SerializeField] private string playerIdPrefsKey = "analytics.player_id";
    [SerializeField] private string apiKeyEnvironmentVariable = "KF_ANALYTICS_API_KEY";
    [SerializeField] private bool requireHmac = true;
    [SerializeField] private int batchSize = 50;
    [SerializeField] private float flushIntervalSeconds = 15;
    [SerializeField] private float sessionTimeoutMinutes = 30;
    [SerializeField] private int retryCount = 3;
    [SerializeField] private float retryBaseDelaySeconds = 0.5f;
    [SerializeField] private float retryMaxDelaySeconds = 30;
    [SerializeField] private int maxQueueSize = 10000;
    [SerializeField] private bool consentRequired;
    [Range(0, 1)]
    [SerializeField] private double samplingRate = 1;

    public string PlayerIdPrefsKey => playerIdPrefsKey;

    public AnalyticsConfig ToConfig()
    {
        var config = new AnalyticsConfig
        {
            Endpoint = new Uri(endpoint.EndsWith("/", StringComparison.Ordinal) ? endpoint : $"{endpoint}/"),
            ProjectId = projectId,
            ApiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable) ?? string.Empty,
            Platform = Application.platform.ToString(),
            Version = Application.version,
            AppVersion = Application.version,
            BuildId = string.IsNullOrWhiteSpace(buildId) ? Application.version : buildId,
            GitSha = Environment.GetEnvironmentVariable(gitShaEnvironmentVariable),
            ContentVersion = contentVersion,
            SdkVersion = "0.1.0",
            RequireHmac = requireHmac,
            BatchSize = batchSize,
            FlushInterval = TimeSpan.FromSeconds(flushIntervalSeconds),
            SessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes),
            RetryCount = retryCount,
            RetryBaseDelay = TimeSpan.FromSeconds(retryBaseDelaySeconds),
            RetryMaxDelay = TimeSpan.FromSeconds(retryMaxDelaySeconds),
            MaxQueueSize = maxQueueSize,
            ConsentRequired = consentRequired,
            SamplingRate = samplingRate,
        };
        config.DefaultContext["platform"] = Application.platform.ToString();
        config.DefaultContext["version"] = Application.version;
        config.DefaultContext["app_version"] = Application.version;
        config.DefaultContext["build_id"] = config.BuildId;
        config.DefaultContext["git_sha"] = config.GitSha;
        config.DefaultContext["content_version"] = config.ContentVersion;
        config.DefaultContext["unityVersion"] = Application.unityVersion;
        config.DefaultContext["sdk"] = "com.knightfantasy.analytics";
        config.DefaultContext["sdk_version"] = config.SdkVersion;
        return config;
    }
}
}
