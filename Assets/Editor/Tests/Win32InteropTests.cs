using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Robit.Tests
{
    public class Win32InteropTests
    {
        [Test]
        [Category("Integration")]
        public void Brightness_CanBeSet_AndRestored()
        {
            int originalBrightness = Win32BrightnessInterop.GetBrightness();
            if (originalBrightness == -1)
                Assert.Ignore("Brightness control not supported on this monitor.");

            try
            {
                int target = originalBrightness == 100 ? 50 : 100;
                bool success = Win32BrightnessInterop.SetBrightness(target);
                if (!success)
                    Assert.Ignore("SetBrightness failed, might lack permissions or support.");

                Assert.AreEqual(target, Win32BrightnessInterop.GetBrightness());
            }
            finally
            {
                Win32BrightnessInterop.SetBrightness(originalBrightness);
            }
        }

        [Test]
        [Category("Integration")]
        public void DisplayScale_CanBeSet_AndRestored()
        {
            int originalScale = Win32DisplayScaleInterop.GetScalePercent();
            if (originalScale == 100 && !Win32DisplayScaleInterop.SetScalePercent(100))
                Assert.Ignore("Display scale control not supported on this setup.");

            try
            {
                // Try 125 if original is 100, else try 100
                int target = originalScale == 100 ? 125 : 100;
                bool success = Win32DisplayScaleInterop.SetScalePercent(target);
                if (success)
                {
                    Assert.AreEqual(target, Win32DisplayScaleInterop.GetScalePercent());
                }
            }
            finally
            {
                Win32DisplayScaleInterop.SetScalePercent(originalScale);
            }
        }

        [Test]
        [Category("Integration")]
        public void AudioVolume_CanBeSet_AndRestored()
        {
            float originalVol = Win32AudioInterop.GetVolume();
            try
            {
                float target = originalVol < 0.5f ? 0.8f : 0.2f;
                Win32AudioInterop.SetVolume(target);
                
                // Allow a tiny margin of float error from COM interop
                float newVol = Win32AudioInterop.GetVolume();
                Assert.IsTrue(Mathf.Abs(newVol - target) < 0.05f, $"Expected ~{target}, got {newVol}");
            }
            finally
            {
                Win32AudioInterop.SetVolume(originalVol);
            }
        }

        [Test]
        [Category("Integration")]
        public void AudioMute_CanBeSet_AndRestored()
        {
            bool originalMute = Win32AudioInterop.GetMute();
            try
            {
                Win32AudioInterop.SetMute(!originalMute);
                Assert.AreNotEqual(originalMute, Win32AudioInterop.GetMute());
            }
            finally
            {
                Win32AudioInterop.SetMute(originalMute);
            }
        }

        [Test]
        [Category("Smoke")]
        public void WindowManager_Methods_DoNotThrowInEditor()
        {
            // Because WindowManager methods are wrapped in #if !UNITY_EDITOR,
            // calling them here should simply no-op and not throw exceptions.
            Assert.DoesNotThrow(() => WindowManager.Initialize());
            Assert.DoesNotThrow(() => WindowManager.MakeTransparent());
            Assert.DoesNotThrow(() => WindowManager.MakeOpaque());
            Assert.DoesNotThrow(() => WindowManager.SetClickThrough(true));
            Assert.DoesNotThrow(() => WindowManager.SetAcrylicBlur(true));
            Assert.DoesNotThrow(() => WindowManager.MakeFullscreen());
            Assert.DoesNotThrow(() => WindowManager.FocusWindow());
            Assert.DoesNotThrow(() => WindowManager.FocusWindowBehind());
            
            // Assert handle is Zero in editor
            Assert.AreEqual(IntPtr.Zero, WindowManager.GetWindowHandle());
        }

        [Test]
        [Category("Unit")]
        public void WindowsPopupSuppressor_StartAndStop_DoesNotThrow()
        {
            var go = new GameObject("PopupSuppressor");
            
            // UnityNativeBridge MUST be added first so the NativeLifetimeCoordinator is initialized.
            // The popup suppressor requires it to safely pin its P/Invoke delegates.
            var bridge = go.AddComponent<UnityNativeBridge>();
            
            // Unity lifecycle methods like Awake() hit DontDestroyOnLoad which crashes in EditMode tests.
            // We bypass Awake entirely and inject the required dependencies directly using Reflection.
            var coordinator = new NativeLifetimeCoordinator();
            typeof(UnityNativeBridge).GetField("_coordinator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(bridge, coordinator);
            typeof(UnityNativeBridge).GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, bridge);
            
            var suppressor = go.AddComponent<WindowsPopupSuppressor>();

            try
            {
                Assert.DoesNotThrow(() => suppressor.StartSuppressing());
                // Let it run for a brief moment
                Thread.Sleep(100);
                Assert.DoesNotThrow(() => suppressor.StopSuppressing());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                
                // Clean up the static instance so we don't pollute other tests
                typeof(UnityNativeBridge).GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, null);
            }
        }
    }
}
