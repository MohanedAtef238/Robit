// Assembly: Robit.PlayModeTests
// Covers:  BaseProcessRunner<T> — singleton, lifecycle, BuildProcessStartInfo
// Methodology:
//   Black-box unit tests on the contract exposed by the abstract base class,
//   exercised through a minimal concrete stub (FakeRunner) that satisfies the
//   two abstract members (LogPrefix + StopRunner) without any real process logic.
//
// WHY PLAYMODE: BaseProcessRunner is a MonoBehaviour; AddComponent<T>() and the
//   Awake() / OnDestroy() messages require a running Unity player loop.
//
// COVERAGE GOALS
//   ✔ Singleton: first instance sets Instance; second destroys itself
//   ✔ Singleton: destroying the holder nulls Instance
//   ✔ OnAfterAwake hook: called once per successful Awake
//   ✔ StopRunner: called by OnDestroy
//   ✔ BuildProcessStartInfo: all flags match the contract documented on the method

using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Robit.PlayModeTests
{
    // ─── Minimal concrete stub ────────────────────────────────────────────────
    // FakeRunner lives only in the test assembly, satisfying BaseProcessRunner's
    // two abstract members with the bare minimum needed to instantiate it.

    public class FakeRunner : BaseProcessRunner<FakeRunner>
    {
        protected override string LogPrefix => "[FakeRunner]";

        public int  StopRunnerCallCount  { get; private set; }
        public int  AfterAwakeCallCount  { get; private set; }

        protected override void OnAfterAwake() => AfterAwakeCallCount++;

        public override void StopRunner() => StopRunnerCallCount++;

        // Expose BuildProcessStartInfo to the test assembly
        public static ProcessStartInfo PublicBuildPSI(
            string exePath, string args, bool stdin = false)
            => BuildProcessStartInfo(exePath, args, stdin);
    }

    // ─── Test class ───────────────────────────────────────────────────────────

    public class ProcessRunnerTests
    {
        private GameObject _holderA;
        private FakeRunner _runnerA;

        [SetUp]
        public void Setup()
        {
            // Each test starts with one clean FakeRunner instance.
            _holderA  = new GameObject("FakeRunnerA");
            _runnerA  = _holderA.AddComponent<FakeRunner>();
        }

        [TearDown]
        public void Teardown()
        {
            // Destroy any leftover GameObjects so the static Instance is cleared
            // between tests and tests do not contaminate each other.
            if (_holderA != null)
                Object.DestroyImmediate(_holderA);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Singleton contract — first instance is registered
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if Instance is not set in Awake(), all callers that
        /// read FakeRunner.Instance (or GazeFollowerRunner.Instance, etc.) will
        /// get null and throw a NullReferenceException at the call site.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Awake_SetsInstance_ToSelf()
        {
            Assert.AreSame(_runnerA, FakeRunner.Instance,
                "First AddComponent<FakeRunner>() must set Instance to itself.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Singleton contract — second instance destroys itself (not its holder)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if the singleton guard is missing, a scene reload
        /// creates a duplicate runner that overwrites Instance, causing the
        /// original (and all its live coroutines / process handles) to become
        /// an orphan while callers silently talk to the new, uninitialized one.
        /// Test type: Integration
        /// </summary>
        [Test]
        [Category("Integration")]
        public void SecondInstance_DestroysItself_InstanceRemainsFirst()
        {
            var holderB = new GameObject("FakeRunnerB");
            holderB.AddComponent<FakeRunner>();

            // Instance must still be the FIRST runner, not the second.
            Assert.AreSame(_runnerA, FakeRunner.Instance,
                "Second AddComponent<FakeRunner> must not overwrite Instance.");

            // The second holder's component is destroyed; the GO itself may
            // still exist briefly — clean it up.
            Object.DestroyImmediate(holderB);
        }

        // ─────────────────────────────────────────────────────────────────────
        // OnAfterAwake hook — called exactly once per successful instantiation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: UiAutomationRunner relies on OnAfterAwake() to
        /// conditionally call StartAutomationServer(). If the hook is never
        /// invoked, autostart silently fails with no error.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void OnAfterAwake_IsCalledOnce_AfterSuccessfulAwake()
        {
            Assert.AreEqual(1, _runnerA.AfterAwakeCallCount,
                "OnAfterAwake() must be called exactly once when the singleton is first established.");
        }

        /// <summary>
        /// Risk mitigated: a duplicate runner whose Awake() returns early via
        /// the singleton guard must NOT call OnAfterAwake(), otherwise the
        /// autostart logic in the subclass would execute on the wrong object.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void OnAfterAwake_IsNotCalled_OnDuplicateInstance()
        {
            var holderB  = new GameObject("FakeRunnerB");
            var runnerB  = holderB.AddComponent<FakeRunner>();

            // runnerB triggered the guard path, so its counter must be 0.
            Assert.AreEqual(0, runnerB.AfterAwakeCallCount,
                "OnAfterAwake() must NOT be called when Awake() exits via the duplicate-guard path.");

            Object.DestroyImmediate(holderB);
        }

        // ─────────────────────────────────────────────────────────────────────
        // OnDestroy — Instance is cleared and StopRunner is called
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if OnDestroy() does not null Instance, a destroyed
        /// runner's static reference lingers. The next AddComponent call finds
        /// Instance != null, destroys the new instance, and the system can never
        /// restart without a full domain reload.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void OnDestroy_NullsInstance()
        {
            Object.DestroyImmediate(_holderA);
            _holderA = null; // Prevent TearDown double-destroy

            Assert.IsNull(FakeRunner.Instance,
                "Instance must be null after the holding GameObject is destroyed.");
        }

        /// <summary>
        /// Risk mitigated: external processes must be stopped when the scene
        /// tears down. If StopRunner() is not called from OnDestroy(), processes
        /// like unity_gaze_bridge.exe or unity_emg_bridge.exe become zombies that
        /// hold the webcam or COM port, blocking the next launch.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void OnDestroy_CallsStopRunner()
        {
            // Capture reference before destroying the holder.
            var runner = _runnerA;

            Object.DestroyImmediate(_holderA);
            _holderA = null; // Prevent TearDown double-destroy

            Assert.AreEqual(1, runner.StopRunnerCallCount,
                "StopRunner() must be called exactly once when the holder is destroyed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuildProcessStartInfo — flag contract
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if UseShellExecute is accidentally set true, Unity
        /// cannot redirect stdout/stderr (InvalidOperationException at process
        /// start) and all Python-side logs become invisible to the Unity console.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BuildProcessStartInfo_UseShellExecute_IsFalse()
        {
            var psi = FakeRunner.PublicBuildPSI(@"C:\fake\app.exe", "--port 0");
            Assert.IsFalse(psi.UseShellExecute,
                "UseShellExecute must be false — required for stdout/stderr redirection.");
        }

        /// <summary>
        /// Risk mitigated: if stdout redirect is missing, OnOutputDataReceived
        /// is never called, Python logs never appear, and CALIBRATION_DONE is
        /// never received — the calibration state machine stalls permanently.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BuildProcessStartInfo_RedirectsStdout_AndStderr()
        {
            var psi = FakeRunner.PublicBuildPSI(@"C:\fake\app.exe", "--port 0");
            Assert.IsTrue(psi.RedirectStandardOutput,
                "RedirectStandardOutput must be true to receive Python stdout.");
            Assert.IsTrue(psi.RedirectStandardError,
                "RedirectStandardError must be true to receive Python stderr.");
        }

        /// <summary>
        /// Risk mitigated: if CreateNoWindow is false, a console window flashes
        /// on screen every time a Python bridge is started, which is unacceptable
        /// for a production OS-overlay application.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BuildProcessStartInfo_CreateNoWindow_IsTrue()
        {
            var psi = FakeRunner.PublicBuildPSI(@"C:\fake\app.exe", "--port 0");
            Assert.IsTrue(psi.CreateNoWindow,
                "CreateNoWindow must be true — no console window should appear in the overlay.");
        }

        /// <summary>
        /// Risk mitigated: if WorkingDirectory is not derived from the
        /// executable path, relative file access inside the Python script
        /// (e.g. loading a gaze model from './models/') will fail with
        /// FileNotFoundException because the CWD points to the wrong folder.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BuildProcessStartInfo_WorkingDirectory_IsDirectoryOfExe()
        {
            string exePath = @"C:\StreamingAssets\Multimodal_UDP\unity_gaze_bridge.exe";
            var    psi     = FakeRunner.PublicBuildPSI(exePath, "--port 0");

            Assert.AreEqual(
                Path.GetDirectoryName(exePath),
                psi.WorkingDirectory,
                "WorkingDirectory must equal the directory containing the executable.");
        }

        /// <summary>
        /// Risk mitigated: RedirectStandardInput defaults to false.
        /// Only the camera-check process uses it for the QUIT handshake.
        /// If it were always true, all other processes receive an unexpected
        /// stdin handle and some runtimes (e.g. .NET 6) close stdout/stderr
        /// prematurely when stdin EOF is signalled.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BuildProcessStartInfo_StdinRedirect_OffByDefault_OnByRequest()
        {
            var psiNoStdin   = FakeRunner.PublicBuildPSI(@"C:\fake\app.exe", "", false);
            var psiWithStdin = FakeRunner.PublicBuildPSI(@"C:\fake\app.exe", "", true);

            Assert.IsFalse(psiNoStdin.RedirectStandardInput,
                "RedirectStandardInput must be false when redirectStdin=false.");
            Assert.IsTrue(psiWithStdin.RedirectStandardInput,
                "RedirectStandardInput must be true when redirectStdin=true.");
        }
    }
}
