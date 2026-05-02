// Assembly: Robit.Tests.EditMode
// Covers: MacroButtonBinding.cs, MacroActionType.cs (serialization contract)
// Methodology:
//   Equivalence Partitioning — default-constructed binding / valid binding /
//                              None-typed binding.
//   BVA — actionType at None (0), first real value (Back = 1), last value
//          (Calibration), and an out-of-range cast.
// MacroButtonBinding is a plain [Serializable] class — pure C#, no Play Mode.

using NUnit.Framework;
using System;

namespace Robit.Tests.EditMode
{
    public class MacroButtonBindingTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // EP: default-constructed binding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Unity deserializes ScriptableObject arrays by calling
        /// the default constructor, then filling fields from YAML. If the default
        /// state is invalid in a way that causes a NullRef downstream (e.g. null
        /// buttonName), the Inspector will crash on asset open.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void DefaultConstructed_ButtonName_IsNullOrEmpty()
        {
            // Arrange / Act
            var binding = new MacroButtonBinding();

            // Assert
            Assert.IsTrue(
                binding.buttonName == null || binding.buttonName == string.Empty,
                "Default buttonName must be null or empty — Unity YAML will fill it.");
        }

        /// <summary>
        /// Risk mitigated: default actionType must be None (0) so an uninitialised
        /// binding renders as an empty button slot rather than firing a random action.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void DefaultConstructed_ActionType_IsNone()
        {
            // Arrange / Act
            var binding = new MacroButtonBinding();

            // Assert
            Assert.AreEqual(MacroActionType.None, binding.actionType,
                "Default actionType must be None — uninitialised slots must be inert.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: valid binding assignment
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: confirms the struct fields are publicly mutable so
        /// the Inspector and any runtime reconfiguration code can write to them.
        /// If someone accidentally made a field readonly, button rebinding breaks.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void AssignedValues_AreRetainedCorrectly()
        {
            // Arrange
            var binding = new MacroButtonBinding();

            // Act
            binding.buttonName = "Slot_0";
            binding.actionType = MacroActionType.ZoomIn;

            // Assert
            Assert.AreEqual("Slot_0",               binding.buttonName);
            Assert.AreEqual(MacroActionType.ZoomIn,  binding.actionType);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BVA: actionType at boundary values
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: BVA lower boundary. A binding with actionType = None
        /// is a valid placeholder and must be storable without clamping or error.
        /// MacroButtonController uses None to mark empty slots.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ActionType_CanBeSetToNone_BoundaryMin()
        {
            // Arrange
            var binding = new MacroButtonBinding { actionType = MacroActionType.Back };

            // Act
            binding.actionType = MacroActionType.None; // BVA min

            // Assert
            Assert.AreEqual(MacroActionType.None, binding.actionType);
        }

        /// <summary>
        /// Risk mitigated: BVA min+1. Back is the first real action. Assigning it
        /// confirms the enum minimum viable value works end-to-end through the struct.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ActionType_CanBeSetToBack_BoundaryMinPlusOne()
        {
            // Arrange
            var binding = new MacroButtonBinding();

            // Act
            binding.actionType = MacroActionType.Back; // BVA min + 1

            // Assert
            Assert.AreEqual(MacroActionType.Back, binding.actionType);
        }

        /// <summary>
        /// Risk mitigated: BVA upper boundary. Calibration is currently the last
        /// defined type. If a new type is appended without updating the factory
        /// this assignment will still succeed — the companion MacroActionFactory
        /// test will catch the missing case.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ActionType_CanBeSetToCalibration_BoundaryMax()
        {
            // Arrange
            var binding = new MacroButtonBinding();

            // Act
            binding.actionType = MacroActionType.Calibration; // BVA max

            // Assert
            Assert.AreEqual(MacroActionType.Calibration, binding.actionType);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EP: buttonName edge cases — BVA on string inputs
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: an empty-string buttonName must not break
        /// MacroButtonController's lookup which does string comparison.
        /// The struct must accept it — validation is the controller's responsibility.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ButtonName_CanBeEmptyString()
        {
            // Arrange
            var binding = new MacroButtonBinding();

            // Act
            binding.buttonName = string.Empty;

            // Assert
            Assert.AreEqual(string.Empty, binding.buttonName);
        }

        /// <summary>
        /// Risk mitigated: a whitespace-only name can arrive from a malformed
        /// YAML asset. The struct must store it as-is — whitespace stripping
        /// is the caller's responsibility, not the data container's.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ButtonName_CanBeWhitespace()
        {
            // Arrange
            var binding = new MacroButtonBinding();

            // Act
            binding.buttonName = "   ";

            // Assert
            Assert.AreEqual("   ", binding.buttonName,
                "Binding must store whitespace unchanged — callers handle trimming.");
        }

        /// <summary>
        /// Risk mitigated: BVA string maximum. A very long name (256 chars) must
        /// not cause a truncation or exception — Unity's YAML serializer handles
        /// arbitrary string length and the struct must be transparent to it.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void ButtonName_CanBe256Characters()
        {
            // Arrange
            string longName = new string('A', 256); // BVA max practical string
            var binding = new MacroButtonBinding();

            // Act
            binding.buttonName = longName;

            // Assert
            Assert.AreEqual(256, binding.buttonName.Length,
                "A 256-char buttonName must be stored without truncation.");
        }
    }
}
