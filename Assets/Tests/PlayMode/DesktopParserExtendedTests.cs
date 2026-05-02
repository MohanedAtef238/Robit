// Assembly: Robit.Tests.PlayMode
// Covers: DesktopParser.cs (AddShortcut, IsValidShortcut, ShortcutInfo struct)
// Methodology:
//   Equivalence Partitioning — valid .exe path / non-.exe path / empty path /
//                              null path / whitespace path.
//   Boundary Value Analysis — shortcut list at 0 items, 1 item, and N items.
//                             Name string at empty, 1 char, and max-length.
//
// WHY PLAYMODE: DesktopParser is a MonoBehaviour that uses DontDestroyOnLoad,
// Instance singleton pattern, and StartCoroutine in Start(). These require a
// running Unity player loop. AddComponent<DesktopParser>() and coroutine
// scheduling are not available in EditMode.
//
// Tests that existed in the original DesktopParserTests are NOT duplicated here.
// Specifically: IsValidShortcut_ReturnsTrue_ForExePath,
//               IsValidShortcut_ReturnsFalse_ForNonExePath,
//               IsValidShortcut_ReturnsFalse_ForEmptyPath,
//               AddShortcut_CorrectlyPopulatesList,
//               AddShortcut_HandlesMultipleEntries.
// All tests below are net-new coverage.

using NUnit.Framework;
using UnityEngine;

namespace Robit.Tests.PlayMode
{
    public class DesktopParserExtendedTests
    {
        private GameObject    _holder;
        private DesktopParser _parser;

        [SetUp]
        public void Setup()
        {
            // Arrange (shared)
            _holder = new GameObject("TestDesktopParser");
            _parser = _holder.AddComponent<DesktopParser>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_holder);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: null path input — BVA below minimum
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if a corrupted .lnk yields a null TargetPath and
        /// IsValidShortcut does not guard it, string.EndsWith(".exe") throws
        /// NullReferenceException, crashing the ParseShortcuts coroutine entirely
        /// and leaving the launcher empty. This is the sub-minimum BVA boundary.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsValidShortcut_ReturnsFalse_ForNullPath()
        {
            // Act
            bool result = _parser.IsValidShortcut(null);

            // Assert
            Assert.IsFalse(result,
                "IsValidShortcut must return false for null — prevents NullReferenceException in EndsWith.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: whitespace-only path
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a .lnk whose TargetPath is all whitespace would pass
        /// a naive null check, but File.Exists("   ") returns false, causing
        /// a silent skip downstream. IsValidShortcut should reject it early and
        /// explicitly so the reason is logged.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsValidShortcut_ReturnsFalse_ForWhitespacePath()
        {
            // Arrange
            string whitespace = "   ";

            // Act
            bool result = _parser.IsValidShortcut(whitespace);

            // Assert
            Assert.IsFalse(result,
                "A whitespace-only path must be rejected — it cannot be a valid .exe path.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: path ending in .EXE (upper-case) — case sensitivity
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Windows paths are case-insensitive. A shortcut pointing
        /// to C:\Windows\calc.EXE must be accepted. If EndsWith(".exe") is called
        /// without OrdinalIgnoreCase, uppercase .EXE paths are silently dropped.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsValidShortcut_ReturnsTrue_ForUpperCaseExeExtension()
        {
            // Arrange
            string upperCasePath = "C:\\Windows\\calc.EXE";

            // Act
            bool result = _parser.IsValidShortcut(upperCasePath);

            // Assert
            Assert.IsTrue(result,
                "IsValidShortcut must accept .EXE in any case — Windows paths are case-insensitive.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: path with .exe in the middle, not the extension
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a naive Contains(".exe") check would wrongly accept
        /// "C:\my.exe.folder\document.txt". EndsWith must be used, and this test
        /// confirms the implementation uses the right check.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void IsValidShortcut_ReturnsFalse_ForExeInMiddleOfPath()
        {
            // Arrange
            string exeInMiddle = "C:\\my.exe.folder\\document.txt";

            // Act
            bool result = _parser.IsValidShortcut(exeInMiddle);

            // Assert
            Assert.IsFalse(result,
                "A path where .exe appears in a folder name but not as the file extension must be rejected.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ShortcutInfo struct — field contract (BVA: 1-char name)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: BVA min+1 for Name length. A single-character app name
        /// (e.g. "R") must be stored verbatim. The AppLauncher UI truncates long
        /// names but must never receive an empty string where a 1-char name was given.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void AddShortcut_SingleCharName_IsStoredVerbatim()
        {
            // Arrange
            string singleChar = "R";

            // Act
            _parser.AddShortcut(singleChar, "C:\\R.exe", null);

            // Assert
            Assert.AreEqual(1, _parser.shortcuts.Count);
            Assert.AreEqual(singleChar, _parser.shortcuts[0].Name,
                "A 1-char name must be stored verbatim — no truncation or modification.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: shortcut list at 0 items after construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: AppLauncherUIToolkit iterates shortcuts to build buttons.
        /// If shortcuts is null (not just empty) after construction, the foreach
        /// throws NullReferenceException before Start() populates it.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Shortcuts_IsEmptyList_NotNull_OnConstruction()
        {
            // Assert — _parser was AddComponent'd in SetUp; Start() has not run yet
            Assert.IsNotNull(_parser.shortcuts,
                "shortcuts must be a non-null list after construction — foreach null would throw.");
            Assert.AreEqual(0, _parser.shortcuts.Count,
                "shortcuts must be empty before Start() runs.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: shortcut list after N additions — WorkingDirectory default
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: AddShortcut always sets WorkingDirectory to "".
        /// AppLauncher passes WorkingDirectory to Process.Start — if it were null,
        /// Process.Start throws ArgumentNullException on some .NET runtimes.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void AddShortcut_WorkingDirectory_IsEmptyStringNotNull()
        {
            // Arrange
            _parser.AddShortcut("App", "C:\\App.exe", null);

            // Assert
            Assert.IsNotNull(_parser.shortcuts[0].WorkingDirectory,
                "WorkingDirectory must be non-null — Process.Start throws on null WorkingDirectory.");
            Assert.AreEqual(string.Empty, _parser.shortcuts[0].WorkingDirectory,
                "WorkingDirectory must default to empty string, not null.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // parsingComplete flag — initial state
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: AppLauncherUIToolkit reads parsingComplete to know when
        /// to build the app grid. If it starts true, the grid is built before any
        /// shortcuts are loaded, showing a permanently empty launcher.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ParsingComplete_IsFalse_BeforeCoroutineRuns()
        {
            // Assert — Start() has not been called yet (no yield return null)
            Assert.IsFalse(_parser.parsingComplete,
                "parsingComplete must be false before the ParseShortcuts coroutine finishes.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Singleton — second instance destroys itself
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: DesktopParser uses a singleton pattern. If two instances
        /// exist simultaneously (e.g. scene reload without DontDestroyOnLoad cleanup),
        /// Instance must not be overwritten by the second one. The second instance's
        /// Awake skips the Instance assignment and calls Destroy — Instance stays first.
        /// Test type: Integration
        /// </summary>
        [Test]
        [Category("Integration")]
        public void SecondInstance_DoesNotOverwriteFirstInstance()
        {
            // Arrange — _holder/_parser already exists; Instance was set in its Awake
            var secondHolder = new GameObject("TestDesktopParser2");
            secondHolder.AddComponent<DesktopParser>();

            // Assert — Instance must still point to the first parser, not the second
            Assert.AreSame(_parser, DesktopParser.Instance,
                "The second DesktopParser must not overwrite Instance — singleton must remain the first.");

            // Cleanup
            Object.DestroyImmediate(secondHolder);
        }
    }
}
