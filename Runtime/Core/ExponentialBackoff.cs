using System;

namespace AnalyticsPlatform
{

public static class ExponentialBackoff
{
    public static TimeSpan Delay(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (attempt <= 0 || baseDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var factor = Math.Pow(2, attempt - 1);
        var millis = Math.Min(maxDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * factor);
        return TimeSpan.FromMilliseconds(millis);
    }
}
}
