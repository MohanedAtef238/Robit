using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Robit.Tests
{
    public class EmgPredictionPipeTests
    {
        [Test]
        [Category("Unit")]
        public void PipeRunner_ParsesEmgData_AndUpdatesVirtualInputState()
        {
            var go = new GameObject("EmgPredictionRunnerPipe");
            
            // Bypass Awake via reflection to prevent DontDestroyOnLoad crashes if autoStartOnAwake is true
            var runner = go.AddComponent<EmgPredictionRunnerPipe>();

            // VirtualInputState initializes automatically via its Instance getter
            VirtualInputState.Instance.SetEmgPrediction(false);

            try
            {
                // To simulate receiving console output without launching Python,
                // we instantiate the internal DataReceivedEventArgs using Reflection.
                var eventArgsType = typeof(DataReceivedEventArgs);
                var constructor = eventArgsType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
                var trueArgs = (DataReceivedEventArgs)constructor.Invoke(new object[] { "EMG: 1" });

                // Call the protected OnOutputDataReceived method
                var method = typeof(EmgPredictionRunnerPipe).GetMethod("OnOutputDataReceived", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, "Could not find OnOutputDataReceived method");

                method.Invoke(runner, new object[] { null, trueArgs });

                // The string goes into an internal ConcurrentQueue. We must call Update() to process the queue.
                var updateMethod = typeof(EmgPredictionRunnerPipe).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                updateMethod.Invoke(runner, null);

                // Verify that the String was correctly parsed and pushed to the global state
                Assert.IsTrue(VirtualInputState.Instance.IsEmgActive, "PipeRunner failed to update VirtualInputState from console string 'EMG: 1'");

                // Test parsing '0'
                var falseArgs = (DataReceivedEventArgs)constructor.Invoke(new object[] { "0" });
                method.Invoke(runner, new object[] { null, falseArgs });
                updateMethod.Invoke(runner, null);

                Assert.IsFalse(VirtualInputState.Instance.IsEmgActive, "PipeRunner failed to update VirtualInputState from console string '0'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
