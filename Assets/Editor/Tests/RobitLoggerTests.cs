using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Robit.Tests
{
    public class RobitLoggerTests
    {
        [Test]
        [Category("Unit")]
        public void Log_WritesToDebugLog()
        {
            LogAssert.Expect(LogType.Log, "Test Info Message");
            RobitLogger.Log("Test Info Message");
        }

        [Test]
        [Category("Unit")]
        public void Log_WithContext_WritesToDebugLog()
        {
            var go = new GameObject("TestContext");
            LogAssert.Expect(LogType.Log, "Test Info Message Context");
            RobitLogger.Log("Test Info Message Context", go);
            Object.DestroyImmediate(go);
        }

        [Test]
        [Category("Unit")]
        public void LogWarning_WritesToDebugLogWarning()
        {
            LogAssert.Expect(LogType.Warning, "Test Warning Message");
            RobitLogger.LogWarning("Test Warning Message");
        }

        [Test]
        [Category("Unit")]
        public void LogWarning_WithContext_WritesToDebugLogWarning()
        {
            var go = new GameObject("TestContext");
            LogAssert.Expect(LogType.Warning, "Test Warning Message Context");
            RobitLogger.LogWarning("Test Warning Message Context", go);
            Object.DestroyImmediate(go);
        }

        [Test]
        [Category("Unit")]
        public void LogError_WritesToDebugLogError()
        {
            LogAssert.Expect(LogType.Error, "Test Error Message");
            RobitLogger.LogError("Test Error Message");
        }

        [Test]
        [Category("Unit")]
        public void LogError_WithContext_WritesToDebugLogError()
        {
            var go = new GameObject("TestContext");
            LogAssert.Expect(LogType.Error, "Test Error Message Context");
            RobitLogger.LogError("Test Error Message Context", go);
            Object.DestroyImmediate(go);
        }
    }
}
