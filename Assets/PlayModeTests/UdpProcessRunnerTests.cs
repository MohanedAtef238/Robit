// Assembly: Robit.PlayModeTests
// Covers:  BaseUdpProcessRunner<T>.SetupUdpAndGetPort() + CleanupUdp()
//          EmgPredictionRunner UDP message parsing (CurrentSensorStatus, queue)
// Methodology:
//   The test acts as the Python process: it sends raw UDP datagrams to the
//   loopback port claimed by the runner, then asserts that the runner's public
//   state reflects the parsed payload correctly.
//
//   This requires NO real Python executable — it exercises only the C# side
//   of the IPC channel, which is the part we own and can break.
//
// WHY PLAYMODE: EmgPredictionRunner is a MonoBehaviour; its Update() dequeues
//   packets into pythonEmgActive state. Frame yields are needed to drive this.
//
// COVERAGE GOALS
//   ✔ SetupUdpAndGetPort returns a valid loopback port (> 0, < 65536)
//   ✔ CleanupUdp nulls the socket so a second CleanupUdp call is a safe no-op
//   ✔ EMG_STATUS: OK sets CurrentSensorStatus
//   ✔ EMG_STATUS: ERROR sets CurrentSensorStatus
//   ✔ Payload "1" enqueues true into emgPacketQueue (visible after one Update)
//   ✔ Payload "0" enqueues false into emgPacketQueue (visible after one Update)
//   ✔ Unknown payload is silently ignored (no exception, status unchanged)

using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Robit.PlayModeTests
{
    // ─── Minimal UDP stub ─────────────────────────────────────────────────────
    // Exposes SetupUdpAndGetPort and CleanupUdp as public so tests can call
    // them without relying on StartEmgPrediction (which requires a real .exe).

    public class FakeUdpRunner : BaseUdpProcessRunner<FakeUdpRunner>
    {
        protected override string LogPrefix => "[FakeUdpRunner]";
        public override void StopRunner() => CleanupUdp();

        public int  CallSetupUdp() => SetupUdpAndGetPort();
        public void CallCleanupUdp() => CleanupUdp();

        // Expose internal socket state for teardown validation
        public bool IsUdpClientNull => udpClient == null;

        protected override System.Threading.Tasks.Task ReceiveUdpLoop(
            System.Threading.CancellationToken token)
            => Task.CompletedTask; // No-op — tests don't need this path
    }

    public class UdpProcessRunnerTests
    {
        private GameObject       _emgHolder;
        private EmgPredictionRunner _emg;

        private GameObject    _fakeHolder;
        private FakeUdpRunner _fakeRunner;

        [SetUp]
        public void Setup()
        {
            _fakeHolder = new GameObject("FakeUdpRunnerHolder");
            _fakeRunner = _fakeHolder.AddComponent<FakeUdpRunner>();

            _emgHolder  = new GameObject("EmgRunner");
            _emg        = _emgHolder.AddComponent<EmgPredictionRunner>();
        }

        [TearDown]
        public void Teardown()
        {
            // CleanupUdp before destroy so background tasks finish gracefully
            _fakeRunner?.CallCleanupUdp();
            if (_fakeHolder != null) Object.DestroyImmediate(_fakeHolder);

            if (_emgHolder  != null) Object.DestroyImmediate(_emgHolder);
        }

        // ─── Helper — sends a raw UDP packet to a loopback port ──────────────

        private static async Task SendUdpAsync(int port, string payload)
        {
            using var sender = new UdpClient();
            byte[] data      = Encoding.UTF8.GetBytes(payload);
            await sender.SendAsync(data, data.Length,
                new IPEndPoint(IPAddress.Loopback, port));
        }

        // ─── Helper — get the assigned loopback port from the running EMG runner
        //    via the protected udpClient field using reflection

        private static int GetEmgPort(EmgPredictionRunner runner)
        {
            var field = typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetField("udpClient",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var client = (UdpClient)field!.GetValue(runner);
            return ((IPEndPoint)client.Client.LocalEndPoint).Port;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SetupUdpAndGetPort — port range contract
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if SetupUdpAndGetPort returns 0, the process is
        /// launched with --port 0, which Python interprets as "OS-assigned" and
        /// binds to a *different* random port — the two ends can never talk.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void SetupUdpAndGetPort_ReturnsValidLoopbackPort()
        {
            int port = _fakeRunner.CallSetupUdp();

            Assert.Greater(port, 0,   "Assigned port must be greater than 0.");
            Assert.Less   (port, 65536, "Assigned port must be a valid TCP/UDP port (< 65536).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // CleanupUdp — idempotency
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: StopGazeFollower and StopEmgPrediction both call
        /// CleanupUdp(). OnDestroy() also calls StopRunner() → CleanupUdp().
        /// If CleanupUdp is not idempotent, the second call throws
        /// ObjectDisposedException, which bubbles out of OnDestroy and
        /// prevents Instance from being cleared.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void CleanupUdp_IsIdempotent_NeverThrows()
        {
            _fakeRunner.CallSetupUdp();
            _fakeRunner.CallCleanupUdp();

            Assert.DoesNotThrow(
                () => _fakeRunner.CallCleanupUdp(),
                "Calling CleanupUdp() twice must not throw — StopRunner + OnDestroy both call it.");
        }

        /// <summary>
        /// Risk mitigated: callers check udpClient != null before calling
        /// SendAsync(). If CleanupUdp leaves a non-null disposed socket,
        /// those callers attempt a send on a closed socket and throw
        /// SocketException, crashing the calibration send path.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void CleanupUdp_NullsUdpClient_AfterCleanup()
        {
            _fakeRunner.CallSetupUdp();
            _fakeRunner.CallCleanupUdp();

            Assert.IsTrue(_fakeRunner.IsUdpClientNull,
                "udpClient must be null after CleanupUdp() so callers know the socket is gone.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EmgPredictionRunner — EMG_STATUS: OK parsing
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: GazeCalibrationController queries CurrentSensorStatus
        /// to gate the "proceed with EMG-assisted calibration" decision. If the
        /// OK status string is not parsed correctly (e.g. extra whitespace, wrong
        /// prefix), the controller stays in the waiting state indefinitely and
        /// the user can never start calibration.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator EmgStatusOk_Payload_SetsCurrentSensorStatus()
        {
            // The runner needs to be actually listening — prime its UDP socket
            // by calling StartEmgPrediction's equivalent setup directly via
            // reflection to avoid needing the real .exe.
            typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetMethod("SetupUdpAndGetPort",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_emg, null);

            int port = GetEmgPort(_emg);

            // Act — send the exact string the Python bridge emits on successful
            // sensor connection (see unity_emg_bridge.py)
            SendUdpAsync(port, "EMG_STATUS: OK").GetAwaiter().GetResult();

            // Give the background receive task time to wake up
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual("OK", _emg.CurrentSensorStatus,
                "CurrentSensorStatus must be 'OK' after receiving 'EMG_STATUS: OK'.");
        }

        /// <summary>
        /// Risk mitigated: the error status path exists to log a human-readable
        /// failure to the Unity console. If it is not parsed, the operator sees
        /// "UNKNOWN" and has no way to diagnose why EMG is silent.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator EmgStatusError_Payload_SetsCurrentSensorStatus()
        {
            typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetMethod("SetupUdpAndGetPort",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_emg, null);

            int port = GetEmgPort(_emg);

            SendUdpAsync(port, "EMG_STATUS: ERROR_COM4_UNAVAILABLE")
                .GetAwaiter().GetResult();

            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual("ERROR_COM4_UNAVAILABLE", _emg.CurrentSensorStatus,
                "CurrentSensorStatus must reflect the full error token from the Python bridge.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EmgPredictionRunner — binary "1" / "0" prediction parsing
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: the main prediction signal is a "1" or "0" sent 30×/s
        /// from Python. If the parse is broken, pythonEmgActive never becomes true
        /// and the EMG overlay gesture is permanently disabled regardless of
        /// actual muscle activity.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Payload_1_EnqueuesTrue_Into_EmgPacketQueue()
        {
            typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetMethod("SetupUdpAndGetPort",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_emg, null);

            int port = GetEmgPort(_emg);

            SendUdpAsync(port, "1").GetAwaiter().GetResult();

            // Wait for the background receive task and then one Update() frame
            yield return new WaitForSeconds(0.1f);
            yield return null;

            // Inspect the ConcurrentQueue via reflection because it is private
            var queueField = typeof(EmgPredictionRunner)
                .GetField("emgPacketQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            var queue = queueField!.GetValue(_emg)
                as System.Collections.Concurrent.ConcurrentQueue<bool>;

            // The queue is drained by Update(). After one frame it is empty but
            // pythonEmgActive should be true. Read it via reflection.
            var activeField = typeof(EmgPredictionRunner)
                .GetField("pythonEmgActive",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            bool active = (bool)activeField!.GetValue(_emg);

            Assert.IsTrue(active,
                "pythonEmgActive must be true after receiving payload '1' and one Update() frame.");
        }

        /// <summary>
        /// Risk mitigated: a "0" after a "1" must reset the active state.
        /// If the reset is broken, a single muscle activation permanently locks
        /// the EMG overlay ON and the user cannot dismiss it.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Payload_0_EnqueuesFalse_ResetsActiveState()
        {
            typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetMethod("SetupUdpAndGetPort",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_emg, null);

            int port = GetEmgPort(_emg);

            // First activate, then deactivate
            SendUdpAsync(port, "1").GetAwaiter().GetResult();
            yield return new WaitForSeconds(0.05f);
            SendUdpAsync(port, "0").GetAwaiter().GetResult();
            yield return new WaitForSeconds(0.1f);
            yield return null; // Drive Update()

            var activeField = typeof(EmgPredictionRunner)
                .GetField("pythonEmgActive",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            bool active = (bool)activeField!.GetValue(_emg);

            Assert.IsFalse(active,
                "pythonEmgActive must be false after receiving '0' following a '1'.");
        }

        /// <summary>
        /// Risk mitigated: the ReceiveUdpLoop must silently skip unrecognised
        /// payloads. If an unknown payload throws an unhandled exception, the
        /// entire receive task terminates and all subsequent packets are dropped —
        /// effectively killing the gaze and EMG bridges permanently.
        /// Test type: Unit
        /// </summary>
        [UnityTest]
        [Category("Unit")]
        public IEnumerator UnknownPayload_IsIgnored_StatusRemainsUnknown()
        {
            typeof(BaseUdpProcessRunner<EmgPredictionRunner>)
                .GetMethod("SetupUdpAndGetPort",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_emg, null);

            int port = GetEmgPort(_emg);

            SendUdpAsync(port, "GARBAGE_PAYLOAD_XYZ")
                .GetAwaiter().GetResult();

            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual("UNKNOWN", _emg.CurrentSensorStatus,
                "An unrecognised payload must be silently dropped — status must remain UNKNOWN.");
        }
    }
}
