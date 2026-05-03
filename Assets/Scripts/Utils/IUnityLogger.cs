public interface IUnityLogger
{
    void Log(object message);
    void LogWarning(object message);
    void LogError(object message);
}

public class UnityLogger : IUnityLogger
{
    public void Log(object message) => UnityEngine.Debug.Log(message);
    public void LogWarning(object message) => UnityEngine.Debug.LogWarning(message);
    public void LogError(object message) => UnityEngine.Debug.LogError(message);
}
