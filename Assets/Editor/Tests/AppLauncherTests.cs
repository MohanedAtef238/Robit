using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Robit.LauncherSystem;
using UnityEngine.TestTools;

namespace Robit.Tests
{
    public class AppLauncherTests
    {
        private GameObject _launcherObject;
        private global::AppLauncher _launcher;
        private MockProcessRunner _mockRunner;
        private MockSceneLoader _mockLoader;

        [SetUp]
        public void SetUp()
        {
            // Arrange (shared) — create the launcher and call Initialize()
            // directly instead of relying on Unity to call Awake() in EditMode.
            // Initialize() is internal and visible via [assembly: InternalsVisibleTo].
            _launcherObject = new GameObject("AppLauncher");
            _launcher = _launcherObject.AddComponent<AppLauncher>();
            _launcher.Initialize();

            _mockRunner = new MockProcessRunner();
            _mockLoader = new MockSceneLoader();

            _launcher.ProcessRunner = _mockRunner;
            _launcher.SceneLoader   = _mockLoader;
        }

        [TearDown]
        public void TearDown()
        {
            // Reset singleton so the next test starts clean.
            // Instance is internal — visible here via InternalsVisibleTo.
            AppLauncher.Instance = null;
            Object.DestroyImmediate(_launcherObject);
        }

        // ── Singleton ─────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a second AppLauncher created on scene reload must
        /// destroy itself and leave the first instance as the singleton.
        /// If both survive, LaunchApplication fires twice per call.
        /// </summary>
        [Test]
        public void Singleton_SecondInstance_DestroysItselfAndPreservesFirst()
        {
            // Arrange
            var secondObject  = new GameObject("AppLauncherDuplicate");
            var secondLauncher = secondObject.AddComponent<AppLauncher>();

            // Act — Initialize plays the role Awake() would in Play Mode
            secondLauncher.Initialize();

            // Assert
            Assert.AreEqual(_launcher, AppLauncher.Instance,
                "First instance must remain the singleton after a second Initialize().");

            // secondObject should have been destroyed by DestroyImmediate inside Initialize()
            Assert.IsTrue(
                secondObject == null || secondObject.Equals(null),
                "Second AppLauncher must destroy its own GameObject in Initialize().");
        }

        // ── LaunchApplication ─────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: LaunchApplication must call ProcessRunner.Start with
        /// the correct path and then load the overlay scene. If either is skipped,
        /// the user sees no app or no overlay.
        /// </summary>
        [Test]
        public void LaunchApplication_ValidPath_CallsRunnerThenLoadsOverlay()
        {
            // Arrange
            string testPath = "C:\\Windows\\notepad.exe";
            string testDir  = "C:\\Windows";

            // Act
            _launcher.LaunchApplication(testPath, testDir);

            // Assert
            Assert.AreEqual(testPath,      _mockRunner.LastStartedPath,
                "ProcessRunner.Start must receive the exact path passed to LaunchApplication.");
            Assert.AreEqual("OverlayScene", _mockLoader.LastLoadedScene,
                "LoadScene must be called with 'OverlayScene' after a successful launch.");
        }

        /// <summary>
        /// Risk mitigated: if ProcessRunner.Start throws (e.g. bad path, missing exe),
        /// LaunchApplication must catch the exception and NOT load the overlay scene.
        /// Loading the overlay with no running process leaves the UI in a broken state.
        /// </summary>
        [Test]
        public void LaunchApplication_RunnerThrows_DoesNotLoadScene()
        {
            // Arrange
            _mockRunner.ShouldFail = true;

            // This error is EXPECTED as we are testing the failure recovery path.
            // LogAssert.Expect tells Unity to ignore this error in the test results.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to launch application.*Mock Launch Failure"));

            // Act
            _launcher.LaunchApplication("bad.exe", "");

            // Assert
            Assert.IsNull(_mockLoader.LastLoadedScene,
                "Scene must NOT be loaded when ProcessRunner.Start throws.");
        }

        /// <summary>
        /// Risk mitigated: launching a second app while one is already running must
        /// close the first process before starting the new one. Leaving orphaned
        /// processes alive wastes resources and can block ports or file handles.
        /// </summary>
        [Test]
        public void LaunchApplication_WithExistingProcess_ClosesOldProcessFirst()
        {
            // Arrange — launch a first app
            _launcher.LaunchApplication("first.exe", "");
            var firstProcess = _mockRunner.LastStartedProcess;
            Assert.IsNotNull(firstProcess, "First process must be set after first launch.");

            // Act — launch a second app
            _mockRunner.Reset();
            _launcher.LaunchApplication("second.exe", "");

            // Assert
            Assert.IsTrue(_mockRunner.CloseCalled,
                "ProcessRunner.Close must be called for the existing process before starting a new one.");
        }

        // ── CloseCurrentApp ───────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: CloseCurrentApp must call ProcessRunner.Close and then
        /// clear CurrentProcess to null. If CurrentProcess is left non-null after
        /// closing, LaunchApplication will try to close it again on the next launch,
        /// calling Close on an already-dead process.
        /// </summary>
        [Test]
        public void CloseCurrentApp_AfterLaunch_ClosesProcessAndClearsReference()
        {
            // Arrange
            _launcher.LaunchApplication("test.exe", "");
            Assert.IsNotNull(_launcher.CurrentProcess,
                "Pre-condition: CurrentProcess must be set after launch.");

            // Act
            _launcher.CloseCurrentApp();

            // Assert
            Assert.IsTrue(_mockRunner.CloseCalled,
                "ProcessRunner.Close must be called.");
            Assert.IsNull(_launcher.CurrentProcess,
                "CurrentProcess must be null after CloseCurrentApp().");
        }

        /// <summary>
        /// Risk mitigated: CloseCurrentApp called when no app is running must be
        /// a safe no-op. If it throws NullReferenceException, the Home macro button
        /// crashes the overlay on first press after the user closes their app manually.
        /// </summary>
        [Test]
        public void CloseCurrentApp_WhenNoProcessRunning_DoesNotThrow()
        {
            // Arrange — no LaunchApplication called, CurrentProcess is null

            // Act / Assert
            Assert.DoesNotThrow(
                () => _launcher.CloseCurrentApp(),
                "CloseCurrentApp() with no running process must be a safe no-op.");
        }
    }
}
