using System;
using AnalyticsPlatform;
using UnityEngine;

namespace AnalyticsPlatform.Unity
{

public sealed class UnityLogSink : ILogSink
{
    public void Info(string message)
    {
        Debug.Log(message);
    }

    public void Warning(string message)
    {
        Debug.LogWarning(message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Debug.LogError(exception == null ? message : $"{message} {exception.Message}");
    }
}
}
