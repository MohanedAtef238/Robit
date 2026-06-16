using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Robit.Tests
{
    public class WindowsPopupSuppressorTests
    {
        private GameObject _go;
        private WindowsPopupSuppressor _suppressor;
        private GameObject _bridgeGo;

        [SetUp]
        public void SetUp()
        {
            // Set up native bridge which is required by PopupSuppressor
            _bridgeGo = new GameObject("UnityNativeBridge");
            var bridge = _bridgeGo.AddComponent<UnityNativeBridge>();
            // Unity lifecycle methods like Awake() hit DontDestroyOnLoad which crashes in EditMode tests.
            // We bypass Awake entirely and inject the required dependencies directly using Reflection.
            var coordinator = new NativeLifetimeCoordinator();
            typeof(UnityNativeBridge).GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bridge, coordinator);
            typeof(UnityNativeBridge).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, bridge);
            _go = new GameObject("PopupSuppressor");
            _suppressor = _go.AddComponent<WindowsPopupSuppressor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_bridgeGo != null)
            {
                var destroyMethod = typeof(UnityNativeBridge).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
                destroyMethod.Invoke(_bridgeGo.GetComponent<UnityNativeBridge>(), null);
                UnityEngine.Object.DestroyImmediate(_bridgeGo);
            }
        }

        [Test]
        public void StartSuppressing_AndStopSuppressing_ManageState()
        {
            // Act
            _suppressor.StartSuppressing();
            
            // Assert field _running
            var runningField = typeof(WindowsPopupSuppressor).GetField("_running", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)runningField.GetValue(_suppressor));

            // Stop
            _suppressor.StopSuppressing();
            Assert.IsFalse((bool)runningField.GetValue(_suppressor));
        }

        [Test]
        public void OnEnumWindow_ReturnsFalse_WhenShutdownDetected()
        {
            // Destroy the bridge to cause InvalidOperationException when Coordinator is accessed
            var destroyMethod = typeof(UnityNativeBridge).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            destroyMethod.Invoke(_bridgeGo.GetComponent<UnityNativeBridge>(), null);

            // Act
            var onEnumMethod = typeof(WindowsPopupSuppressor).GetMethod("OnEnumWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            
            LogAssert.Expect(LogType.Log, "[PopupSuppressor] Shutdown detected mid-enumeration \u2014 aborting.");
            bool result = (bool)onEnumMethod.Invoke(_suppressor, new object[] { IntPtr.Zero, IntPtr.Zero });

            // Assert
            Assert.IsFalse(result, "Should return false to abort enumeration during shutdown.");
        }

        [Test]
        public void OnEnumWindow_ReturnsTrue_ForInvalidWindowHandle()
        {
            // Act - Passing IntPtr.Zero is an invalid window, so IsWindowVisible should return false.
            var onEnumMethod = typeof(WindowsPopupSuppressor).GetMethod("OnEnumWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            bool result = (bool)onEnumMethod.Invoke(_suppressor, new object[] { IntPtr.Zero, IntPtr.Zero });

            // Assert
            Assert.IsTrue(result, "Should return true to continue enumeration for invisible/invalid windows.");
        }

        [Test]
        public void OnDestroy_CallsStopSuppressing()
        {
            // Arrange
            _suppressor.StartSuppressing();
            var runningField = typeof(WindowsPopupSuppressor).GetField("_running", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)runningField.GetValue(_suppressor));

            // Act
            var destroyMethod = typeof(WindowsPopupSuppressor).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            destroyMethod.Invoke(_suppressor, null);

            // Assert
            Assert.IsFalse((bool)runningField.GetValue(_suppressor), "StopSuppressing should have been called.");
        }
    }
}
