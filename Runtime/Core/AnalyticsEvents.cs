using System.Collections.Generic;

namespace AnalyticsPlatform
{

public static class AnalyticsEvents
{
    public static bool TrackPerformanceSample(
        this IAnalytics analytics,
        double fps,
        string scene,
        double? frameTimeMs = null,
        string? deviceTier = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["fps"] = fps,
            ["scene"] = scene,
        };
        Add(properties, "frame_time_ms", frameTimeMs);
        Add(properties, "device_tier", deviceTier);
        return analytics.Track("performance.sample", properties);
    }

    public static bool TrackClientError(
        this IAnalytics analytics,
        string code,
        string severity,
        string? scene = null,
        string? message = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["severity"] = severity,
        };
        Add(properties, "scene", scene);
        Add(properties, "message", message);
        return analytics.Track("client_error", properties);
    }

    public static bool TrackEconomySource(
        this IAnalytics analytics,
        string currency,
        double amount,
        string reason,
        double? balanceAfter = null)
    {
        return analytics.Track("economy.source", EconomyProperties(currency, amount, reason, null, balanceAfter));
    }

    public static bool TrackEconomySink(
        this IAnalytics analytics,
        string currency,
        double amount,
        string reason,
        string? item = null,
        double? balanceAfter = null)
    {
        return analytics.Track("economy.sink", EconomyProperties(currency, amount, reason, item, balanceAfter));
    }

    public static bool TrackEconomyBalanceSnapshot(
        this IAnalytics analytics,
        string currency,
        double balanceAfter,
        string? reason = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["currency"] = currency,
            ["balance_after"] = balanceAfter,
        };
        Add(properties, "reason", reason);
        return analytics.Track("economy.balance_snapshot", properties);
    }

    public static bool TrackShopOpen(this IAnalytics analytics, string shopId, string? source = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["shop_id"] = shopId,
        };
        Add(properties, "source", source);
        return analytics.Track("shop_open", properties);
    }

    public static bool TrackShopPurchase(
        this IAnalytics analytics,
        string shopId,
        string item,
        string? currency = null,
        double? amount = null,
        string? offerId = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["shop_id"] = shopId,
            ["item"] = item,
        };
        Add(properties, "currency", currency);
        Add(properties, "amount", amount);
        Add(properties, "offer_id", offerId);
        return analytics.Track("shop_purchase", properties);
    }

    public static bool TrackAdRewardClaimed(
        this IAnalytics analytics,
        string placement,
        string reward,
        double? amount = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["placement"] = placement,
            ["reward"] = reward,
        };
        Add(properties, "amount", amount);
        return analytics.Track("ad_reward_claimed", properties);
    }

    public static bool TrackOfferShown(this IAnalytics analytics, string offerId, string? placement = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["offer_id"] = offerId,
        };
        Add(properties, "placement", placement);
        return analytics.Track("offer_shown", properties);
    }

    public static bool TrackOfferAccepted(this IAnalytics analytics, string offerId, string? placement = null)
    {
        var properties = new Dictionary<string, object?>
        {
            ["offer_id"] = offerId,
        };
        Add(properties, "placement", placement);
        return analytics.Track("offer_accepted", properties);
    }

    private static Dictionary<string, object?> EconomyProperties(
        string currency,
        double amount,
        string reason,
        string? item,
        double? balanceAfter)
    {
        var properties = new Dictionary<string, object?>
        {
            ["currency"] = currency,
            ["amount"] = amount,
            ["reason"] = reason,
        };
        Add(properties, "item", item);
        Add(properties, "balance_after", balanceAfter);
        return properties;
    }

    private static void Add(IDictionary<string, object?> properties, string key, object? value)
    {
        if (value is not null)
        {
            properties[key] = value;
        }
    }
}
}
