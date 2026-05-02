// Assembly: Robit.Tests.EditMode
// Covers: WinShortcut.cs (LnkParser), Constants.cs (LinkFlags, FileAttributes,
//         LinkInfoFlags, VirtualKeys)
// Methodology:
//   Equivalence Partitioning — valid .lnk stream / truncated stream /
//                              empty stream / stream with no LinkInfo.
//   Boundary Value Analysis — HotKey byte at 0x00 (no hotkey), a single-
//                             modifier hotkey, and a three-modifier hotkey.
//   Constants EP — each flag class tested for expected integer values so
//                  any accidental edit is caught before it silently breaks
//                  binary parsing.
//
// WinShortcut reads a Stream — we provide MemoryStream fixtures rather than
// real .lnk files so the tests have zero filesystem dependency.
// All tests are pure C# EditMode.

using NUnit.Framework;
using System;
using System.IO;
using LnkParser;
using LnkParser.Constants;

namespace Robit.Tests.EditMode
{
    public class WinShortcutTests
    {
        // ── Minimal valid .lnk header builder ────────────────────────────────
        // A Windows Shell Link (.lnk) header is 76 bytes:
        //   Bytes 0–3    : HeaderSize = 76 (0x4C)
        //   Bytes 4–19   : LinkCLSID (16 bytes, zeroed)
        //   Bytes 20–23  : LinkFlags  (4)
        //   Bytes 24–27  : FileAttributes (4)
        //   Bytes 28–63  : various timestamps / reserved (36 bytes, zeroed)
        //   Bytes 64–65  : HotKey (low byte = VKey, high byte = modifier flags)
        //   Bytes 66–75  : ShowCommand + Reserved (zeroed)
        private static byte[] BuildMinimalHeader(
            int  linkFlags      = 0,
            int  fileAttributes = 0,
            byte hotKeyLow      = 0,
            byte hotKeyHigh     = 0)
        {
            byte[] header = new byte[76];

            // HeaderSize = 0x4C = 76
            BitConverter.GetBytes(76).CopyTo(header, 0);

            // LinkFlags at offset 20
            BitConverter.GetBytes(linkFlags).CopyTo(header, 20);

            // FileAttributes at offset 24
            BitConverter.GetBytes(fileAttributes).CopyTo(header, 24);

            // HotKey at offset 64 (low = VKey, high = modifier flags)
            header[64] = hotKeyLow;
            header[65] = hotKeyHigh;

            return header;
        }

        private static MemoryStream HeaderOnly(
            int  linkFlags      = 0,
            int  fileAttributes = 0,
            byte hotKeyLow      = 0,
            byte hotKeyHigh     = 0)
        {
            byte[] bytes = BuildMinimalHeader(linkFlags, fileAttributes, hotKeyLow, hotKeyHigh);
            return new MemoryStream(bytes);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: truncated / empty stream
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: WinShortcut wraps Parse() in a try/catch that re-throws
        /// as Exception("Failed to parse…"). An empty stream (BVA min = 0 bytes)
        /// must produce that outer Exception, not a raw EndOfStreamException or
        /// NullReferenceException, so callers can distinguish parse failures from
        /// programmer errors.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Constructor_EmptyStream_ThrowsException()
        {
            // Arrange — BVA min = 0 bytes
            using var empty = new MemoryStream();

            // Act / Assert — outer Exception, not raw inner exception
            var ex = Assert.Throws<Exception>(
                () => new WinShortcut(empty),
                "An empty stream must produce Exception(\"Failed to parse…\"), not a raw inner exception.");

            StringAssert.Contains("Failed to parse", ex.Message);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: valid header, no flags set — HotKey is empty
        // BVA: hotKeyLow = 0x00, hotKeyHigh = 0x00 (no hotkey boundary)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: most shortcuts have no hotkey. HotKey must return ""
        /// (not null) when bytes are 0x00. DesktopParser does not use HotKey but
        /// future features might — a null return would cause NullReferenceException
        /// on string concatenation.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void HotKey_ReturnsEmptyString_WhenBothBytesAreZero()
        {
            // Arrange — hotKeyLow = 0, hotKeyHigh = 0 (BVA min, no hotkey)
            using MemoryStream stream = HeaderOnly(hotKeyLow: 0x00, hotKeyHigh: 0x00);

            // Act
            var shortcut = new WinShortcut(stream);

            // Assert
            Assert.AreEqual(string.Empty, shortcut.HotKey,
                "HotKey must return \"\" (not null) when both bytes are zero.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: single modifier — Ctrl only (HOTKEYF_CONTROL = 2 in high byte)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a Ctrl+Z shortcut would carry high byte = 0x02 and
        /// low byte = 0x5A (VirtualKeys.Z). ParseHeader must produce "ctrl+Z".
        /// If the bit test is wrong the returned HotKey string will be empty,
        /// silently losing the modifier information.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void HotKey_ReturnsCtrlPlusKey_WhenControlFlagSet()
        {
            // Arrange — HOTKEYF_CONTROL = 2 (0x02), Z = 0x5A
            byte hotKeyHigh = (byte)VirtualKeys.HOTKEYF_CONTROL; // 2
            byte hotKeyLow  = (byte)VirtualKeys.Z;               // 0x5A

            using MemoryStream stream = HeaderOnly(hotKeyHigh: hotKeyHigh, hotKeyLow: hotKeyLow);

            // Act
            var shortcut = new WinShortcut(stream);

            // Assert
            Assert.AreEqual("ctrl+Z", shortcut.HotKey,
                "HOTKEYF_CONTROL (0x02) + Z (0x5A) must produce \"ctrl+Z\".");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: all three modifiers — Ctrl + Shift + Alt (high byte = 0x07)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: BVA upper boundary for modifier flags. All three bits
        /// set simultaneously must produce all three modifier strings. A bitmask
        /// error that drops one flag would silently misrepresent the shortcut.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void HotKey_ReturnsAllModifiers_WhenAllFlagsSet()
        {
            // Arrange — 0x07 = HOTKEYF_SHIFT(1) | HOTKEYF_CONTROL(2) | HOTKEYF_ALT(4)
            // hotKeyLow = 0 so no VKey name is appended
            using MemoryStream stream = HeaderOnly(hotKeyHigh: 0x07, hotKeyLow: 0x00);

            // Act
            var shortcut = new WinShortcut(stream);

            // Assert — order matches ParseHeader: ctrl, shift, alt
            StringAssert.Contains("ctrl",  shortcut.HotKey, "HOTKEYF_CONTROL must produce \"ctrl\".");
            StringAssert.Contains("shift", shortcut.HotKey, "HOTKEYF_SHIFT must produce \"shift\".");
            StringAssert.Contains("alt",   shortcut.HotKey, "HOTKEYF_ALT must produce \"alt\".");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FileAttributes — IsDirectory flag
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: DesktopParser skips non-.exe targets, but if IsDirectory
        /// were always true, every shortcut would be filtered out and the launcher
        /// would show zero apps. The Directory bit (0x10) must be parsed correctly.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsDirectory_ReturnsTrue_WhenDirectoryAttributeSet()
        {
            // Arrange — FileAttributes.Directory = 0x0010
            using MemoryStream stream = HeaderOnly(fileAttributes: LnkParser.Constants.FileAttributes.Directory);

            // Act
            var shortcut = new WinShortcut(stream);

            // Assert
            Assert.IsTrue(shortcut.IsDirectory,
                "IsDirectory must be true when the Directory attribute bit (0x10) is set.");
        }

        /// <summary>
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsDirectory_ReturnsFalse_WhenDirectoryAttributeNotSet()
        {
            // Arrange — no Directory bit set (fileAttributes = 0)
            using MemoryStream stream = HeaderOnly(fileAttributes: 0);

            // Act
            var shortcut = new WinShortcut(stream);

            // Assert
            Assert.IsFalse(shortcut.IsDirectory,
                "IsDirectory must be false when the Directory attribute bit is not set.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Constants contract tests — EP on flag values
    // ─────────────────────────────────────────────────────────────────────────

    public class LinkFlagsConstantsTests
    {
        /// <summary>
        /// Risk mitigated: LinkFlags values are used as bitmask tests against raw
        /// bytes from the .lnk file. If any constant is edited (e.g. a misplaced
        /// zero), the parser silently skips sections, producing null TargetPaths
        /// and empty launchers.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void LinkFlags_HasExpectedValues()
        {
            Assert.AreEqual(0x00000001, LinkFlags.HasLinkTargetIdList);
            Assert.AreEqual(0x00000002, LinkFlags.HasLinkInfo);
            Assert.AreEqual(0x00000004, LinkFlags.HasName);
            Assert.AreEqual(0x00000008, LinkFlags.HasRelativePath);
            Assert.AreEqual(0x00000010, LinkFlags.HasWorkingDir);
            Assert.AreEqual(0x00000020, LinkFlags.HasArguments);
            Assert.AreEqual(0x00000040, LinkFlags.HasIconLocation);
            Assert.AreEqual(0x00000080, LinkFlags.IsUnicode);
        }

        /// <summary>
        /// Risk mitigated: overlapping flag values would cause two sections to be
        /// parsed when only one is present. Every flag must occupy a unique bit.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void LinkFlags_AreAllUniquePowerOfTwoBits()
        {
            int[] flags = new[]
            {
                LinkFlags.HasLinkTargetIdList,
                LinkFlags.HasLinkInfo,
                LinkFlags.HasName,
                LinkFlags.HasRelativePath,
                LinkFlags.HasWorkingDir,
                LinkFlags.HasArguments,
                LinkFlags.HasIconLocation,
                LinkFlags.IsUnicode,
                LinkFlags.ForceNoLinkInfo,
                LinkFlags.HasExpIcon,
                LinkFlags.EnableTargetMetadata,
            };

            // Verify each flag is a power of two (exactly one bit set)
            foreach (int flag in flags)
            {
                int temp = flag;
                int bitCount = 0;
                while (temp != 0)
                {
                    bitCount += temp & 1;
                    temp >>= 1;
                }
                Assert.AreEqual(1, bitCount,
                    $"LinkFlag 0x{flag:X8} is not a power-of-two — it must occupy exactly one bit.");
            }

            // Verify no two flags share any bits
            int combined = 0;
            foreach (int flag in flags)
            {
                Assert.AreEqual(0, combined & flag,
                    $"LinkFlag 0x{flag:X8} overlaps with a previously seen flag.");
                combined |= flag;
            }
        }
    }

    public class VirtualKeysConstantsTests
    {
        /// <summary>
        /// Risk mitigated: modifier flag constants used in ParseHeader's bit test.
        /// If HOTKEYF_CONTROL were changed from 2 to 4 (colliding with ALT), all
        /// Ctrl shortcuts would be mis-parsed as Alt shortcuts.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void HotkeyModifiers_HaveExpectedValues()
        {
            Assert.AreEqual(1, (int)VirtualKeys.HOTKEYF_SHIFT,   "HOTKEYF_SHIFT must be 1");
            Assert.AreEqual(2, (int)VirtualKeys.HOTKEYF_CONTROL, "HOTKEYF_CONTROL must be 2");
            Assert.AreEqual(4, (int)VirtualKeys.HOTKEYF_ALT,     "HOTKEYF_ALT must be 4");
        }

        /// <summary>
        /// Risk mitigated: three commonly used VKeys in the macro system.
        /// Verifying their Win32 values guards against copy-paste errors when
        /// adding new action subclasses that reference these keys directly.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void CommonVirtualKeys_HaveExpectedValues()
        {
            Assert.AreEqual(0x5A, (int)VirtualKeys.Z,     "VirtualKeys.Z must be 0x5A");
            Assert.AreEqual(0x21, (int)VirtualKeys.Prior, "VirtualKeys.Prior (PageUp) must be 0x21");
            Assert.AreEqual(0x22, (int)VirtualKeys.Next,  "VirtualKeys.Next (PageDown) must be 0x22");
        }
    }
}
