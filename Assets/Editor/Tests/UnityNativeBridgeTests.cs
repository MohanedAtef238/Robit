using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Reflection;

namespace Robit.Tests
{
    public class UnityNativeBridgeTests
    {
        private GameObject _bridgeObject;
        private UnityNativeBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _bridgeObject = new GameObject("UnityNativeBridge");
            _bridge = _bridgeObject.AddComponent<UnityNativeBridge>();
            
            // Inject dependencies directly since Awake() uses DontDestroyOnLoad which crashes in EditMode
            var coordinator = new NativeLifetimeCoordinator();
            typeof(UnityNativeBridge).GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_bridge, coordinator);
            typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, _bridge);
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridgeObject != null)
            {
                Object.DestroyImmediate(_bridgeObject);
            }
            
            // Ensure instance is cleared
            var field = typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, null);
        }

        [Test]
        public void Coordinator_Throws_WhenNotInitialized()
        {
            // Arrange
            var field = typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, null); // Clear it

            // Act & Assert
            var ex = Assert.Throws<System.InvalidOperationException>(() => { var c = UnityNativeBridge.Coordinator; });
            StringAssert.Contains("UnityNativeBridge is not initialized", ex.Message);
        }

        [Test]
        public void Coordinator_ReturnsInstance_WhenInitialized()
        {
            Assert.IsNotNull(UnityNativeBridge.Coordinator);
        }

        [Test]
        public void Awake_DestroysDuplicateInstance()
        {
            // Arrange
            var duplicateObject = new GameObject("DuplicateBridge");
            var duplicateBridge = duplicateObject.AddComponent<UnityNativeBridge>();

            // Act
            var awakeMethod = typeof(UnityNativeBridge).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Unity's Destroy() method logs an error when called in EditMode, which we expect here.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
            
            awakeMethod.Invoke(duplicateBridge, null);

            // Assert
            // duplicateObject should be destroyed (in EditMode, Destroy doesn't happen instantly, it queues it, 
            // but we can check the instance hasn't changed)
            var field = typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var currentInstance = field.GetValue(null);
            
            Assert.AreEqual(_bridge, currentInstance);
            
            // Cleanup
            Object.DestroyImmediate(duplicateObject);
        }

        [Test]
        public void OnApplicationQuit_CallsShutdownAndDrain()
        {
            // Arrange
            var quitMethod = typeof(UnityNativeBridge).GetMethod("OnApplicationQuit", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            LogAssert.Expect(LogType.Log, "[UnityNativeBridge] Native resources drained for shutdown.");
            quitMethod.Invoke(_bridge, null);

            // Assert
            Assert.Pass("Shutdown called without crashing.");
        }

        [Test]
        public void OnDestroy_CleansUpInstance()
        {
            // Act
            var destroyMethod = typeof(UnityNativeBridge).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            destroyMethod.Invoke(_bridge, null);

            // Assert
            var field = typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var currentInstance = field.GetValue(null);
            
            Assert.IsNull(currentInstance);
        }
    }
}
