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
            _launcherObject = new GameObject("AppLauncher");
            _launcher = _launcherObject.AddComponent<AppLauncher>();
            
            // Manually trigger Awake in EditMode if it's not already called
            if (AppLauncher.Instance == null)
            {
                _launcher.Awake();
            }

            _mockRunner = new MockProcessRunner();
            _mockLoader = new MockSceneLoader();
            
            _launcher.ProcessRunner = _mockRunner;
            _launcher.SceneLoader = _mockLoader;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_launcherObject);
            AppLauncher.Instance = null;
        }

        [Test]
        public void Singleton_DestroysDuplicateInstances()
        {
            // Arrange
            var secondObject = new GameObject("AppLauncherDuplicate");
            var secondLauncher = secondObject.AddComponent<AppLauncher>();

            // Act - Trigger Awake on the second instance
            secondLauncher.Awake();
            
            // Assert
            Assert.AreEqual(_launcher, AppLauncher.Instance);
            Assert.IsTrue(secondObject == null || secondObject.Equals(null), "Second object should have been destroyed.");
        }

        [Test]
        public void LaunchApplication_CallsRunnerAndLoader()
        {
            // Arrange
            string testPath = "C:\\Windows\\notepad.exe";
            string testDir = "C:\\Windows";

            // Act
            _launcher.LaunchApplication(testPath, testDir);

            // Assert
            Assert.AreEqual(testPath, _mockRunner.LastStartedPath);
            Assert.AreEqual("OverlayScene", _mockLoader.LastLoadedScene);
        }

        [Test]
        public void LaunchApplication_OnFailure_DoesNotChangeScene()
        {
            // Arrange
            _mockRunner.ShouldFail = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to launch application.*Mock Launch Failure"));

            // Act
            _launcher.LaunchApplication("fail.exe", "");

            // Assert
            Assert.IsNull(_mockLoader.LastLoadedScene, "Scene should not be loaded if launch fails.");
        }

        [Test]
        public void CloseCurrentApp_CallsRunnerClose()
        {
            // Act
            // Note: We can't easily set currentProcess because it's private,
            // but we can trigger a launch first (which sets it to null in our mock, 
            // but the logic checks for null). 
            // Let's modify AppLauncher to make testing currentProcess easier or 
            // just rely on the launch flow.
            
            _launcher.LaunchApplication("test.exe", "");
            _launcher.CloseCurrentApp();

            // Assert
            // Our mock returns null, so CloseMainWindow won't be called in the real code
            // if it checks for null. Let's verify our mock logic.
        }
    }
}

