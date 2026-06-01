using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsPlatform;

namespace AnalyticsPlatform.Unity
{

public static class Analytics
{
    private static IAnalytics? _instance;

    public static bool IsInitialized => _instance != null;

    public static void Initialize(IAnalytics analytics)
    {
        _instance = analytics;
    }

    public static void Identify(string userId)
    {
        _instance?.Identify(userId);
    }

    public static bool Track(string name, IReadOnlyDictionary<string, object?>? properties = null)
    {
        return _instance?.Track(name, properties) ?? false;
    }

    public static ExperimentVariant GetVariant(string experimentKey)
    {
        return _instance?.GetVariant(experimentKey) ?? new ExperimentVariant("control", new Dictionary<string, object?>());
    }

    public static T GetParam<T>(string experimentKey, string key, T fallback)
    {
        return _instance is null ? fallback : _instance.GetParam(experimentKey, key, fallback);
    }

    public static void AppForeground()
    {
        _instance?.AppForeground();
    }

    public static void AppBackground()
    {
        _instance?.AppBackground();
    }

    public static void SetConsent(bool granted)
    {
        _instance?.SetConsent(granted);
    }

    public static Task<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        return _instance?.FlushAsync(cancellationToken) ?? Task.FromResult(new FlushResult(0, 0, true));
    }

    public static Task RefreshConfigAsync(CancellationToken cancellationToken = default)
    {
        return _instance?.RefreshConfigAsync(cancellationToken) ?? Task.CompletedTask;
    }
}
}
