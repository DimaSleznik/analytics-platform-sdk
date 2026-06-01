using System;
using AnalyticsPlatform;

namespace AnalyticsPlatform.Unity
{

public sealed class UnityClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
}
