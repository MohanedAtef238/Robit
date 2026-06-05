using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Intermediate base class for runners that communicate with their child process
/// over a loopback UDP socket (currently GazeFollowerRunner and EmgPredictionRunner).
///
/// Extends <see cref="BaseProcessRunner{T}"/> with:
///   - Dynamic loopback UDP port allocation
///   - Background receive loop management
///   - Standardised UDP cleanup
///
/// UiAutomationRunner does NOT inherit from this class because it has no UDP
/// communication, keeping the Interface Segregation Principle intact.
/// </summary>
public abstract class BaseUdpProcessRunner<T> : BaseProcessRunner<T>
    where T : BaseUdpProcessRunner<T>
{
    // Protected so subclasses can access them directly (e.g. to close in their
    // StopRunner() implementation, or to send data back to the process).
    protected UdpClient                 udpClient;
    protected CancellationTokenSource   udpCancellation;

    // ── UDP lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a loopback UDP socket on an OS-assigned port, starts the
    /// background <see cref="ReceiveUdpLoop"/> task, and returns the assigned
    /// port number so the caller can pass it to the child process via --port.
    ///
    /// Must be called before the child process is started.
    /// </summary>
    protected int SetupUdpAndGetPort()
    {
        udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int assignedPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;

        udpCancellation = new CancellationTokenSource();
        _ = Task.Run(
            () => ReceiveUdpLoop(udpCancellation.Token),
            udpCancellation.Token);

        return assignedPort;
    }

    /// <summary>
    /// Background UDP receive loop.  Runs on a thread-pool thread.
    /// Each concrete class implements its own message parsing logic here.
    ///
    /// The implementation must:
    ///   - Loop while <paramref name="token"/> is not cancelled
    ///   - Catch <see cref="ObjectDisposedException"/> and break (socket closed)
    ///   - Catch other exceptions and log/continue rather than crash the loop
    /// </summary>
    protected abstract Task ReceiveUdpLoop(CancellationToken token);

    /// <summary>
    /// Cancels the receive loop and disposes the UDP socket.
    /// Safe to call multiple times or when UDP was never set up.
    /// </summary>
    protected void CleanupUdp()
    {
        if (udpCancellation != null)
        {
            udpCancellation.Cancel();
            udpCancellation.Dispose();
            udpCancellation = null;
        }

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient.Dispose();
            udpClient = null;
        }
    }
}
