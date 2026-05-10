// Assembly: Robit.Tests.PlayMode
// Covers: AppCyclerAction.Execute() — scene navigation behaviour
// Methodology:
//   Scenario test — triggering the action must result in HomeScene becoming
//   the active scene. This replaces the deleted CyclerLogicTests /
//   CyclerStateManagerTests which tested the now-removed AppCycler UI dock.
//
// WHY PLAYMODE: SceneManager.LoadScene() requires a running Unity player loop.
// The scene transition only takes effect at the end of the current frame, so
// a yield is needed before asserting the active scene name.

using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Robit.Tests.PlayMode
{
    public class AppCyclerActionTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Scenario: Execute loads the App Launcher scene
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if Execute() is updated to load the wrong scene name
        /// or reverts to the old AppCyclerController lookup, the macro silently
        /// does nothing and the user cannot reach the app launcher.
        /// Test type: Integration (scene transition)
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Execute_LoadsHomeScene()
        {
            var action = new AppCyclerAction();

            action.Execute();

            // Scene load takes effect at the end of the frame.
            yield return null;

            Assert.AreEqual("HomeScene", SceneManager.GetActiveScene().name,
                "AppCyclerAction.Execute() must load the 'HomeScene' scene.");
        }
    }
}
