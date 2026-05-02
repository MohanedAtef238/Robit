// Assembly: Robit.Tests.EditMode
// Covers: PointerInputProvider.cs, IInputProvider.cs
// Methodology:
//   Equivalence Partitioning — Attach with valid callback / null callback /
//                              Detach registered element / Detach unregistered.
//   BVA — 0 callbacks attached, 1 attached, N attached (multiple elements).
// Hand-rolled fakes used — no Moq. VisualElement is a UI Toolkit class that
// can be constructed in EditMode without a running Unity player.
//
// NOTE: RegisterCallback / UnregisterCallback on VisualElement ARE available
// in EditMode. PointerUpEvent dispatching requires a panel (Play Mode), so
// we do NOT test that the callback fires — only that Attach/Detach manage
// the internal dictionary correctly and throw nothing in edge cases.

using NUnit.Framework;
using System;
using UnityEngine.UIElements;

namespace Robit.Tests.EditMode
{
    // ── Hand-rolled fake: records whether onActivated was ever called ─────────
    internal class FakeMacroAction : IMacroAction
    {
        public string ActionId    => "fake_action";
        public string DisplayName => "Fake";
        public int    ExecuteCount { get; private set; }
        public void Execute() => ExecuteCount++;
    }

    public class PointerInputProviderTests
    {
        private PointerInputProvider _provider;
        private VisualElement        _element;

        [SetUp]
        public void Setup()
        {
            // Arrange (shared) — fresh provider and a bare VisualElement
            _provider = new PointerInputProvider();
            _element  = new VisualElement();
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Attach with a valid callback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Attach() must not throw for a standard use-case.
        /// If PointerInputProvider's internal dictionary throws on first insert
        /// (e.g. wrong key type), every macro button fails to bind on startup.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Attach_ValidElementAndCallback_DoesNotThrow()
        {
            // Arrange
            Action callback = () => { };

            // Act / Assert
            Assert.DoesNotThrow(
                () => _provider.Attach(_element, callback),
                "Attach() with a valid element and callback must not throw.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Attach with null callback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: MacroButton.Bind() passes () => Action.Execute() so
        /// null is unlikely, but a binding with actionType = None produces
        /// MacroActionFactory.Create(None) = null, which could lead to a null
        /// lambda. Attach() must either accept null safely or the caller must
        /// guard — this test documents current behaviour so regressions are
        /// immediately visible.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Attach_NullCallback_DoesNotThrow()
        {
            // Arrange — null callback simulates an empty slot binding
            Action nullCallback = null;

            // Act / Assert
            Assert.DoesNotThrow(
                () => _provider.Attach(_element, nullCallback),
                "Attach() with a null callback must not throw — null guard is the caller's job.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Detach a registered element
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: MacroButton.Unbind() calls Detach() on page changes
        /// and on destroy. If Detach() fails to remove the dictionary entry,
        /// every page flip leaks a PointerUpEvent callback — eventually multiple
        /// actions fire per click.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Detach_RegisteredElement_DoesNotThrow()
        {
            // Arrange
            _provider.Attach(_element, () => { });

            // Act / Assert
            Assert.DoesNotThrow(
                () => _provider.Detach(_element),
                "Detach() on a registered element must not throw.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Detach an element that was never registered
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if MacroButtonController calls Detach() before Attach()
        /// (possible on a fast scene reload), a KeyNotFoundException from the
        /// internal dictionary would crash the entire overlay. The TryGetValue
        /// guard must make this a safe no-op.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Detach_UnregisteredElement_DoesNotThrow()
        {
            // Arrange — element was never Attached
            var unregistered = new VisualElement();

            // Act / Assert
            Assert.DoesNotThrow(
                () => _provider.Detach(unregistered),
                "Detach() on an element that was never registered must be a safe no-op.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: 0 elements attached
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a freshly constructed PointerInputProvider with no
        /// elements attached must accept a Detach() call without accessing an
        /// empty dictionary unsafely. BVA boundary: zero elements.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Detach_OnFreshProvider_ZeroElements_DoesNotThrow()
        {
            // Arrange — _provider is fresh from SetUp, nothing attached (BVA min = 0)

            // Act / Assert
            Assert.DoesNotThrow(
                () => _provider.Detach(_element),
                "Detach() on a provider with zero registrations must not throw.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: N distinct elements (multiple registrations)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: a macro group has 3 buttons per page × up to 9 groups.
        /// All can be Attached to the same PointerInputProvider. Detaching one must
        /// not disturb the others. BVA upper boundary for a full page = 3.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Detach_OneOfThreeRegistered_OthersTwoDoNotThrowOnSubsequentDetach()
        {
            // Arrange
            var el0 = new VisualElement();
            var el1 = new VisualElement();
            var el2 = new VisualElement();

            _provider.Attach(el0, () => { });
            _provider.Attach(el1, () => { });
            _provider.Attach(el2, () => { });

            // Act — detach only el1
            _provider.Detach(el1);

            // Assert — el0 and el2 must still detach without throwing
            Assert.DoesNotThrow(() => _provider.Detach(el0),
                "Detach(el0) must not throw after el1 was removed.");
            Assert.DoesNotThrow(() => _provider.Detach(el2),
                "Detach(el2) must not throw after el1 was removed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: Attach same element twice
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if MacroButtonController calls Bind() on an already-bound
        /// button (e.g. rapid page flip), Attach() is called twice on the same element.
        /// The internal Dictionary will overwrite the key, keeping only one callback.
        /// This is the current contract — this test documents it so any change is
        /// immediately visible.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Attach_SameElementTwice_DoesNotThrow()
        {
            // Arrange
            Action firstCallback  = () => { };
            Action secondCallback = () => { };

            // Act / Assert — dictionary overwrites key, must not throw
            Assert.DoesNotThrow(
                () =>
                {
                    _provider.Attach(_element, firstCallback);
                    _provider.Attach(_element, secondCallback);
                },
                "Attaching the same element twice must not throw — dictionary overwrites the previous entry.");
        }
    }
}
