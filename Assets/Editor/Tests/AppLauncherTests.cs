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
        /// the correct path.
        /// </summary>
        [Test]
        public void LaunchApplication_ValidPath_CallsRunner()
        {
            // Arrange
            string testPath = "C:\\Windows\\notepad.exe";
            string testDir  = "C:\\Windows";

            // Act
            _launcher.LaunchApplication(testPath, testDir);

            // Assert
            Assert.AreEqual(testPath,      _mockRunner.LastStartedPath,
                "ProcessRunner.Start must receive the exact path passed to LaunchApplication.");
        }

        /// <summary>
        /// Risk mitigated: if ProcessRunner.Start throws (e.g. bad path, missing exe),
        /// LaunchApplication must catch the exception safely without crashing.
        /// </summary>
        [Test]
        public void LaunchApplication_RunnerThrows_CatchesSafely()
        {
            // Arrange
            _mockRunner.ShouldFail = true;

            // This error is EXPECTED as we are testing the failure recovery path.
            // Using Regex prevents hidden newline/whitespace matching errors.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Mock Launch Failure"));

            // Act & Assert
            Assert.DoesNotThrow(() => _launcher.LaunchApplication("bad.exe", ""));
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

        private class ThrowingCloseRunner : IProcessRunner
        {
            public IProcess Start(string path, string workingDir) => new MockProcess { HasExited = false };
            public void Close(IProcess process) => throw new System.Exception("Mock Close Failure");
        }

        private class MockProcess : IProcess
        {
            public int Id => 1;
            public string ProcessName => "MockProcess";
            public bool HasExited { get; set; }
            public void Kill() {}
            public void Dispose() {}
            public void WaitForExit() {}
        }

        [Test]
        public void CloseCurrentApp_RunnerThrows_LogsWarningAndClearsReference()
        {
            // Arrange
            _launcher.ProcessRunner = new ThrowingCloseRunner();
            _launcher.LaunchApplication("test.exe", "");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Mock Close Failure"));

            // Act
            Assert.DoesNotThrow(() => _launcher.CloseCurrentApp());

            // Assert
            Assert.IsNull(_launcher.CurrentProcess, "CurrentProcess MUST be cleared even if Close throws an exception.");
        }

        [Test]
        public void GoHome_ClosesProcessAndCallsReturnToDesktop()
        {
            // Arrange
            _launcher.LaunchApplication("test.exe", "");
            
            // Act
            _launcher.GoHome();
            
            // Assert
            Assert.IsNull(_launcher.CurrentProcess);
            Assert.IsTrue(_mockRunner.CloseCalled);
        }

        // ── WindowsProcessRunner Tests ──────────────────────────────────────────

        [Test]
        public void WindowsProcessRunner_StartsAndCloses_Notepad()
        {
            var runner = new WindowsProcessRunner();
            
            // Start a process that is guaranteed to stay alive for a few seconds.
            // On Windows 11, notepad.exe is an execution alias that launches a UWP app and immediately exits!
            // ping.exe will ping localhost for 10 seconds and stay alive.
            var proc = runner.Start("ping.exe", "");
            Assert.IsNotNull(proc, "Process runner should return a valid IProcess");
            
            // Wait briefly to ensure it spun up
            System.Threading.Thread.Sleep(500);
            Assert.IsFalse(proc.HasExited, "Process should still be running");

            // Close it
            runner.Close(proc);
            System.Threading.Thread.Sleep(500); // Give it time to close

            // Verify
            // Once a process is Disposed, calling HasExited throws InvalidOperationException.
            // So if it throws that exact exception, we know it was successfully disposed!
            Assert.Throws<System.InvalidOperationException>(() => { var state = proc.HasExited; }, "Process should have been disposed and killed.");
        }

        // ── DesktopParser Tests ─────────────────────────────────────────────────

        private class MockFileSystem : IFileSystem
        {
            public string GetSpecialFolderPath(System.Environment.SpecialFolder folder) => "C:\\MockDesktop";
            public bool DirectoryExists(string path) => path == "C:\\MockDesktop";
            public string[] GetFiles(string path, string searchPattern, bool allDirectories) 
                => new[] { "C:\\MockDesktop\\fake_shortcut.lnk" };
            
            public byte[] ReadAllBytes(string path) => new byte[0]; // Empty bytes to force failure to ShellLinkResolver
            public bool FileExists(string path) => true; 
        }

        [UnityTest]
        public System.Collections.IEnumerator DesktopParser_ParsesShortcuts_FromMockFileSystem()
        {
            var go = new GameObject("DesktopParser");
            var parser = go.AddComponent<DesktopParser>();
            parser.FileSystem = new MockFileSystem();

            // In EditMode, MonoBehaviour.StartCoroutine does NOT automatically tick.
            // We must extract the enumerator via Reflection and manually iterate it!
            var method = typeof(DesktopParser).GetMethod("ParseShortcuts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enumerator = (System.Collections.IEnumerator)method.Invoke(parser, null);

            while (enumerator.MoveNext())
            {
                yield return null; // Yield back to the UnityTest runner to keep it responsive
            }

            // Because the mock file system returns empty bytes for the .lnk file,
            // WinShortcut.TryParse will fail and it will fallback to ShellLinkResolver.
            // ShellLinkResolver will also fail because the path "C:\MockDesktop\fake_shortcut.lnk" doesn't exist on disk.
            // But we can assert the parser gracefully caught the errors and finished without crashing!
            Assert.IsTrue(parser.parsingComplete);
            Assert.AreEqual(0, parser.shortcuts.Count);

            Object.DestroyImmediate(go);
        }

        // ── ShellLinkResolver Tests ─────────────────────────────────────────────

        [Test]
        public void ShellLinkResolver_ReturnsNull_ForInvalidPath()
        {
            string workDir;
            string target = ShellLinkResolver.Resolve("C:\\non_existent_file_123.lnk", out workDir);
            
            Assert.IsNull(target);
            Assert.IsNull(workDir);
        }

        // ── IconExtractor Tests ─────────────────────────────────────────────────

        [Test]
        public void IconExtractor_ExtractsIcon_FromNotepad()
        {
            // notepad.exe is guaranteed to exist on Windows and has an icon
            string notepadPath = "C:\\Windows\\notepad.exe";
            
            var icoData = global::IconExtractor.ExtractAllIcos(notepadPath);
            
            // If we are running on Windows, it should find an icon.
            // (If not on Windows, or permissions deny access, it might be null/empty,
            // but we can assert it does not crash).
            if (System.IO.File.Exists(notepadPath))
            {
                Assert.IsNotNull(icoData);
            }
        }
    }
}
