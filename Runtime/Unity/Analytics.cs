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

    public static bool TrackPerformanceSample(double fps, string scene, double? frameTimeMs = null, string? deviceTier = null)
    {
        return _instance?.TrackPerformanceSample(fps, scene, frameTimeMs, deviceTier) ?? false;
    }

    public static bool TrackClientError(string code, string severity, string? scene = null, string? message = null)
    {
        return _instance?.TrackClientError(code, severity, scene, message) ?? false;
    }

    public static bool TrackEconomySource(string currency, double amount, string reason, double? balanceAfter = null)
    {
        return _instance?.TrackEconomySource(currency, amount, reason, balanceAfter) ?? false;
    }

    public static bool TrackEconomySink(string currency, double amount, string reason, string? item = null, double? balanceAfter = null)
    {
        return _instance?.TrackEconomySink(currency, amount, reason, item, balanceAfter) ?? false;
    }

    public static bool TrackEconomyBalanceSnapshot(string currency, double balanceAfter, string? reason = null)
    {
        return _instance?.TrackEconomyBalanceSnapshot(currency, balanceAfter, reason) ?? false;
    }

    public static bool TrackShopOpen(string shopId, string? source = null)
    {
        return _instance?.TrackShopOpen(shopId, source) ?? false;
    }

    public static bool TrackShopPurchase(string shopId, string item, string? currency = null, double? amount = null, string? offerId = null)
    {
        return _instance?.TrackShopPurchase(shopId, item, currency, amount, offerId) ?? false;
    }

    public static bool TrackAdRewardClaimed(string placement, string reward, double? amount = null)
    {
        return _instance?.TrackAdRewardClaimed(placement, reward, amount) ?? false;
    }

    public static bool TrackOfferShown(string offerId, string? placement = null)
    {
        return _instance?.TrackOfferShown(offerId, placement) ?? false;
    }

    public static bool TrackOfferAccepted(string offerId, string? placement = null)
    {
        return _instance?.TrackOfferAccepted(offerId, placement) ?? false;
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
