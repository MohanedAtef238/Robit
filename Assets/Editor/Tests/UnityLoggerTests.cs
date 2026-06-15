using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Robit.Tests
{
    public class UnityLoggerTests
    {
        [Test]
        [Category("Unit")]
        public void UnityLogger_Log_WritesToDebugLog()
        {
            var logger = new UnityLogger();
            
            LogAssert.Expect(LogType.Log, "UnityLogger Info Test");
            logger.Log("UnityLogger Info Test");
        }

        [Test]
        [Category("Unit")]
        public void UnityLogger_LogWarning_WritesToDebugWarning()
        {
            var logger = new UnityLogger();
            
            LogAssert.Expect(LogType.Warning, "UnityLogger Warning Test");
            logger.LogWarning("UnityLogger Warning Test");
        }

        [Test]
        [Category("Unit")]
        public void UnityLogger_LogError_WritesToDebugError()
        {
            var logger = new UnityLogger();
            
            LogAssert.Expect(LogType.Error, "UnityLogger Error Test");
            logger.LogError("UnityLogger Error Test");
        }
    }
}
