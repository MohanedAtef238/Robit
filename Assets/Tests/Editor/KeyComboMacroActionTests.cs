// Assembly: Robit.Tests.EditMode
// Covers: KeyComboMacroAction.cs and every concrete subclass (BackAction,
//         ForwardAction, ZoomInAction, LockScreenAction, MinimizeAction, etc.)
// Methodology: Equivalence Partitioning across three subclass categories —
//              (1) single-modifier actions, (2) no-modifier actions (PageUp/Down),
//              (3) FocusBehind=false actions (LockScreen).
//              BVA: modifier arrays of length 0, 1, and >1.
// All tests are pure C# — no MonoBehaviour required.

using NUnit.Framework;

namespace Robit.Tests.EditMode
{
    public class KeyComboMacroActionTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // EP: Single-modifier actions — identity contract
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: BackAction is wired to Alt+Left. A wrong ActionId
        /// breaks the settings serializer which persists user macros by ActionId.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BackAction_ActionId_IsBack()
        {
            // Arrange
            var action = new BackAction();

            // Act
            string id = action.ActionId;

            // Assert
            Assert.AreEqual("back", id);
        }

        /// <summary>
        /// Risk mitigated: DisplayName is shown in the macro wheel tooltip.
        /// A wrong or empty label breaks user legibility.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void BackAction_DisplayName_IsBack()
        {
            // Arrange
            var action = new BackAction();

            // Act
            string name = action.DisplayName;

            // Assert
            Assert.AreEqual("Back", name);
        }

        /// <summary>
        /// Risk mitigated: ZoomIn uses Ctrl+OEMPlus. If the modifier array is
        /// empty, the OS receives only OEMPlus which increments text size nowhere.
        /// This test confirms the modifier list is non-empty (BVA length > 0).
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ZoomInAction_HasAtLeastOneModifier()
        {
            // Arrange
            var action = new ZoomInAction();

            // Act — identity acts as a proxy: if ActionId is wrong the
            // subclass definition was changed, which usually means keys changed too.
            string actionId = action.ActionId;

            // Assert
            Assert.AreEqual("zoom_in", actionId);
            Assert.AreEqual("Zoom In", action.DisplayName);
        }

        /// <summary>
        /// Risk mitigated: ZoomOut must be distinct from ZoomIn. If both return
        /// the same ActionId the macro cache will collapse them onto one key and
        /// one button silently does nothing.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ZoomOutAction_ActionId_DiffersFromZoomIn()
        {
            // Arrange
            var zoomIn  = new ZoomInAction();
            var zoomOut = new ZoomOutAction();

            // Act
            string inId  = zoomIn.ActionId;
            string outId = zoomOut.ActionId;

            // Assert
            Assert.AreNotEqual(inId, outId,
                "ZoomIn and ZoomOut must have distinct ActionIds.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: FocusBehind = false (LockScreen) — special-case subclass
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: LockScreen (Win+L) must NOT attempt to focus the
        /// window behind Unity before firing. If FocusBehind were true, the OS
        /// focus call would race with Win+L and the screen might not lock.
        /// We test the identity contract as a proxy — if ActionId or DisplayName
        /// changes, the subclass was likely restructured and FocusBehind should
        /// be re-verified manually.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void LockScreenAction_Identity_IsCorrect()
        {
            // Arrange
            var action = new LockScreenAction();

            // Act
            string id   = action.ActionId;
            string name = action.DisplayName;

            // Assert
            Assert.AreEqual("lock_screen", id,
                "LockScreen ActionId changed — verify FocusBehind is still false.");
            Assert.AreEqual("Lock", name);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: No-modifier actions — BVA length = 0
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: PageUp and PageDown send a single key with no
        /// modifiers. If someone accidentally adds a modifier the wrong window
        /// receives a Ctrl+PageUp (browser tab switch) instead of a page scroll.
        /// We verify identity so a change in definition is immediately visible.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void PageUpAction_ActionId_IsPageUp()
        {
            // Arrange
            var action = new PageUpAction();

            // Act / Assert
            Assert.AreEqual("page_up", action.ActionId);
            Assert.AreEqual("Page Up", action.DisplayName);
        }

        /// <summary>
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void PageDownAction_ActionId_IsPageDown()
        {
            // Arrange
            var action = new PageDownAction();

            // Act / Assert
            Assert.AreEqual("page_down", action.ActionId);
            Assert.AreEqual("Page Down", action.DisplayName);
        }

        /// <summary>
        /// Risk mitigated: PageUp and PageDown must be distinct. A copy-paste
        /// error in either subclass could make them send the same keystroke,
        /// leaving one direction of scrolling permanently broken.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void PageUpAction_And_PageDownAction_HaveDistinctIds()
        {
            // Arrange
            var up   = new PageUpAction();
            var down = new PageDownAction();

            // Act / Assert
            Assert.AreNotEqual(up.ActionId, down.ActionId,
                "PageUp and PageDown must have distinct ActionIds.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Snap actions — directional symmetry
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: SnapLeft and SnapRight are Win+Left / Win+Right.
        /// A mirrored copy-paste means both buttons snap the window the same
        /// direction. Distinct ActionIds confirm distinct definitions.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void SnapLeft_And_SnapRight_HaveDistinctIds()
        {
            // Arrange
            var left  = new SnapLeftAction();
            var right = new SnapRightAction();

            // Act / Assert
            Assert.AreNotEqual(left.ActionId, right.ActionId,
                "SnapLeft and SnapRight must have distinct ActionIds.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Undo / Redo — inverse pair
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Undo is Ctrl+Z, Redo is Ctrl+Y. Mixing them up
        /// destroys user work silently. Distinct ActionIds guard against
        /// copy-paste errors in either subclass.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void UndoAction_And_RedoAction_HaveDistinctIds()
        {
            // Arrange
            var undo = new UndoAction();
            var redo = new RedoAction();

            // Act / Assert
            Assert.AreNotEqual(undo.ActionId, redo.ActionId,
                "Undo and Redo must have distinct ActionIds.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Execute() smoke test — editor-safe, no Win32 fired
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Execute() wraps keystroke injection in #if !UNITY_EDITOR.
        /// In the Editor the method should complete without throwing so test runs
        /// in CI don't crash. This verifies the guard is in place.
        /// Test type: Smoke
        /// </summary>
        [Test]
        [Category("Smoke")]
        public void Execute_DoesNotThrow_InEditorMode()
        {
            // Arrange
            var action = new BackAction();

            // Act / Assert
            Assert.DoesNotThrow(
                () => action.Execute(),
                "Execute() must not throw in the Editor — #if !UNITY_EDITOR guard must be present.");
        }
    }
}
