using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class RobitLogger
{
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, Object context)
    {
        Debug.Log(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message)
    {
        Debug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message, Object context)
    {
        Debug.LogWarning(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message)
    {
        Debug.LogError(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message, Object context)
    {
        Debug.LogError(message, context);
    }
}
