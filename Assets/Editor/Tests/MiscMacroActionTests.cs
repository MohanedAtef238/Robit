// Assembly: Robit.Tests.EditMode
// Covers: SettingsAction, HomePageAction, MuteToggleAction

using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace Robit.Tests
{
    public class MiscMacroActionTests
    {
        [Test]
        [Category("Unit")]
        public void SettingsAction_Execute_LogsWarning_WhenControllerMissing()
        {
            var action = new SettingsAction();
            LogAssert.Expect(LogType.Warning, "[SettingsAction] No SettingsOverlayController found in scene.");
            action.Execute();
        }

        [Test]
        [Category("Unit")]
        public void HomePageAction_Execute_LogsWarning_WhenControllerMissing()
        {
            var action = new HomePageAction();
            LogAssert.Expect(LogType.Log, "[HomePageAction] Executing HomePage Action via ViewCoordinator.");
            LogAssert.Expect(LogType.Warning, "[HomePageAction] ViewCoordinator not found in scene!");
            action.Execute();
        }

        [Test]
        [Category("Unit")]
        public void MuteToggleAction_Execute_TogglesMuteAndRestores()
        {
            var action = new MuteToggleAction();
            
            // Get original state to restore later
            bool originalState = Win32AudioInterop.GetMute();
            
            // Expect the log for the first toggle
            string expectedState = !originalState ? "muted" : "unmuted";
            LogAssert.Expect(LogType.Log, $"[MacroButton] Executing: mute_toggle → {expectedState}");
            
            // Execute toggles it once
            action.Execute();
            
            // Verify it changed
            Assert.AreNotEqual(originalState, Win32AudioInterop.GetMute());
            
            // Restore original state silently or via another execute (which logs again)
            string restoreState = originalState ? "muted" : "unmuted";
            LogAssert.Expect(LogType.Log, $"[MacroButton] Executing: mute_toggle → {restoreState}");
            action.Execute();
            
            // Verify restored
            Assert.AreEqual(originalState, Win32AudioInterop.GetMute());
        }
    }
}
