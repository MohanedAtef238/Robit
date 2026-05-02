// Assembly: Robit.Tests.EditMode
// Covers: HiddenWindowTracker.cs
// Methodology:
//   Equivalence Partitioning — valid handle / zero handle / duplicate handle.
//   Boundary Value Analysis — IntPtr.Zero (min), IntPtr(1) (min+1),
//                             a mid-range handle, IntPtr(int.MaxValue) (max
//                             representable in a 32-bit HWND on Win32).
// Note: RestoreAll() calls Win32Interop.ShowWindow which is a real P/Invoke.
//       We call RestoreAll() only to verify state is cleared — we do not assert
//       on ShowWindow's return value because the fake HWND will legitimately fail.
// All tests are pure C# EditMode — no MonoBehaviour required.

using NUnit.Framework;
using System;

namespace Robit.Tests.EditMode
{
    public class HiddenWindowTrackerTests
    {
        [SetUp]
        public void ClearTrackerState()
        {
            // Arrange (shared) — RestoreAll clears internal HashSet regardless
            // of whether Win32 calls succeed, so it is safe to use as a reset.
            HiddenWindowTracker.RestoreAll();
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: valid handle (non-zero)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: the most common path — Track() is called with a real
        /// HWND. If it fails to add to the set, RestoreAll() will not restore the
        /// window and the user is left with a permanently hidden taskbar window.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_ValidHandle_IncreasesTrackedCount()
        {
            // Arrange
            IntPtr handle = new IntPtr(0x1234);

            // Act
            HiddenWindowTracker.Track(handle);

            // Assert
            Assert.AreEqual(1, HiddenWindowTracker.TrackedCount,
                "A valid non-zero handle must be added to the tracked set.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: zero handle (invalid) — BVA minimum
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: IntPtr.Zero is never a valid HWND. Tracking it would
        /// cause ShowWindow(0, SW_SHOW) in RestoreAll() — a no-op Win32 call that
        /// still wastes cycles and pollutes the set. The guard in Track() must
        /// filter it.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_IntPtrZero_DoesNotIncreaseCount()
        {
            // Arrange
            IntPtr zero = IntPtr.Zero; // BVA minimum

            // Act
            HiddenWindowTracker.Track(zero);

            // Assert
            Assert.AreEqual(0, HiddenWindowTracker.TrackedCount,
                "IntPtr.Zero is not a valid HWND and must be ignored by Track().");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: minimum + 1  (lowest valid HWND value)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: IntPtr(1) is the lowest non-zero HWND value. Some
        /// system pseudo-handles use value 1. The tracker must accept it rather
        /// than silently dropping it with an overly broad guard.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_MinPlusOne_IsAccepted()
        {
            // Arrange
            IntPtr handle = new IntPtr(1); // BVA min + 1

            // Act
            HiddenWindowTracker.Track(handle);

            // Assert
            Assert.AreEqual(1, HiddenWindowTracker.TrackedCount,
                "IntPtr(1) is the BVA min+1 boundary — it must be tracked.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: max representable HWND on 32-bit Win32
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: on 64-bit Windows, HWNDs are still 32-bit values.
        /// Passing int.MaxValue as a handle exercises the upper BVA boundary
        /// and confirms the HashSet does not overflow or reject large pointers.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_MaxInt32Handle_IsAccepted()
        {
            // Arrange
            IntPtr handle = new IntPtr(int.MaxValue); // BVA max (32-bit HWND ceiling)

            // Act
            HiddenWindowTracker.Track(handle);

            // Assert
            Assert.AreEqual(1, HiddenWindowTracker.TrackedCount,
                "IntPtr(int.MaxValue) is the BVA upper boundary — it must be tracked.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: duplicate handle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if the same HWND is tracked twice, RestoreAll() would
        /// attempt ShowWindow on it twice. The second call is harmless but wastes
        /// time and inflates the debug log count. The internal HashSet must dedupe.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_SameHandleTwice_CountRemainsOne()
        {
            // Arrange
            IntPtr handle = new IntPtr(0xABCD);

            // Act
            HiddenWindowTracker.Track(handle);
            HiddenWindowTracker.Track(handle);

            // Assert
            Assert.AreEqual(1, HiddenWindowTracker.TrackedCount,
                "Tracking the same HWND twice must not add a duplicate entry.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: multiple distinct handles
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: the suppressor hides multiple popup windows in a
        /// single session. All must be tracked independently so RestoreAll()
        /// restores every one, not just the first.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_MultipleDistinctHandles_AllAreTracked()
        {
            // Arrange
            IntPtr h1 = new IntPtr(0x0001);
            IntPtr h2 = new IntPtr(0x0002);
            IntPtr h3 = new IntPtr(0x0003);

            // Act
            HiddenWindowTracker.Track(h1);
            HiddenWindowTracker.Track(h2);
            HiddenWindowTracker.Track(h3);

            // Assert
            Assert.AreEqual(3, HiddenWindowTracker.TrackedCount,
                "Three distinct handles must all be tracked independently.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // RestoreAll — state transitions
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: RestoreAll() is called on Application.quitting.
        /// If it does not clear the set, a second quitting event (or a test
        /// SetUp reset) would attempt to restore windows that were already
        /// restored, producing duplicate ShowWindow calls.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void RestoreAll_AfterTracking_ClearsCount()
        {
            // Arrange
            HiddenWindowTracker.Track(new IntPtr(0xABCD));
            HiddenWindowTracker.Track(new IntPtr(0xBEEF));
            Assert.AreEqual(2, HiddenWindowTracker.TrackedCount,
                "Pre-condition: two handles must be tracked.");

            // Act
            HiddenWindowTracker.RestoreAll();

            // Assert
            Assert.AreEqual(0, HiddenWindowTracker.TrackedCount,
                "RestoreAll() must clear the tracked set to zero.");
        }

        /// <summary>
        /// Risk mitigated: calling RestoreAll() on an already-empty set must be
        /// a no-op — not throw, not log a misleading "restored 0 windows" message.
        /// Guards against double-call on a fast shutdown path.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void RestoreAll_OnEmptySet_DoesNotThrow()
        {
            // Arrange — tracker is already empty (cleared by SetUp)

            // Act / Assert
            Assert.DoesNotThrow(
                () => HiddenWindowTracker.RestoreAll(),
                "RestoreAll() on an empty set must be a no-op, not throw.");
        }

        /// <summary>
        /// Risk mitigated: new handles tracked after a RestoreAll() must be
        /// accepted normally. The static state must be fully reset, not stuck
        /// in a "hooked" state that rejects new tracking.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Track_AfterRestoreAll_AcceptsNewHandles()
        {
            // Arrange
            HiddenWindowTracker.Track(new IntPtr(0x1234));
            HiddenWindowTracker.RestoreAll();
            Assert.AreEqual(0, HiddenWindowTracker.TrackedCount,
                "Pre-condition: set must be empty after RestoreAll.");

            // Act
            HiddenWindowTracker.Track(new IntPtr(0x5678));

            // Assert
            Assert.AreEqual(1, HiddenWindowTracker.TrackedCount,
                "Track() must accept new handles after a RestoreAll() call.");
        }
    }
}
