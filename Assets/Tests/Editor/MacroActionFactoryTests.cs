// Assembly: Robit.Tests.EditMode
// Covers: MacroActionFactory.cs, MacroActionType.cs, IMacroAction.cs
// Methodology: Equivalence Partitioning (valid type / None / unknown cast),
//              Boundary Value Analysis (first enum value, last enum value,
//              every defined value, one-past-last cast).
// All tests are pure C# — no MonoBehaviour, no Play Mode required.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Robit.Tests.EditMode
{
    public class MacroActionFactoryTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Equivalence partition: MacroActionType.None
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: callers that pass None (e.g. uninitialised bindings)
        /// must receive null, not an exception, so the button renders as empty
        /// rather than crashing the whole macro wheel on startup.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Create_ReturnsNull_WhenTypeIsNone()
        {
            // Arrange
            const MacroActionType type = MacroActionType.None;

            // Act
            IMacroAction result = MacroActionFactory.Create(type);

            // Assert
            Assert.IsNull(result,
                "Create(None) must return null — a missing binding is not an error.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Equivalence partition: every defined, non-None enum member
        // BVA: first value after None, last value in the enum, every value in between
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: adding a new MacroActionType without updating the
        /// factory switch causes a silent ArgumentException at runtime during
        /// button activation. This test catches the gap at compile/test time.
        /// Test type: Integration (exercises all action subclasses via factory)
        /// </summary>
        [Test]
        [Category("Integration")]
        public void Create_ReturnsNonNull_ForEveryDefinedNonNoneType()
        {
            // Arrange
            IEnumerable<MacroActionType> allTypes = Enum
                .GetValues(typeof(MacroActionType))
                .Cast<MacroActionType>()
                .Where(t => t != MacroActionType.None);

            foreach (MacroActionType type in allTypes)
            {
                // Act
                IMacroAction result = MacroActionFactory.Create(type);

                // Assert
                Assert.IsNotNull(result,
                    $"Create({type}) must return a non-null action — factory is missing a case for this type.");
            }
        }

        /// <summary>
        /// Risk mitigated: BVA lower boundary — Back is the first non-None value.
        /// If the switch statement has an off-by-one or fall-through, the first
        /// real action is the most likely victim.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Create_ReturnsBackAction_ForBoundaryMinType()
        {
            // Arrange
            const MacroActionType type = MacroActionType.Back; // first non-None

            // Act
            IMacroAction result = MacroActionFactory.Create(type);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("back", result.ActionId,
                "Back is the BVA lower boundary — its ActionId must be 'back'.");
        }

        /// <summary>
        /// Risk mitigated: BVA upper boundary — Calibration is the last enum
        /// member. If someone appends a new type without a factory case, the
        /// ArgumentException thrown here exposes the gap before it reaches users.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Create_ReturnsCalibrationAction_ForBoundaryMaxType()
        {
            // Arrange
            const MacroActionType type = MacroActionType.Calibration; // last defined

            // Act
            IMacroAction result = MacroActionFactory.Create(type);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("calibration", result.ActionId,
                "Calibration is the BVA upper boundary — ActionId must be 'calibration'.");
        }

        /// <summary>
        /// Risk mitigated: an integer cast beyond the defined enum range must throw
        /// ArgumentException, not silently return null. This guards against corrupt
        /// serialized MacroButtonBinding assets that carry an out-of-range int.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Create_ThrowsArgumentException_ForUndefinedCastBeyondMax()
        {
            // Arrange — BVA: one-past the highest defined enum value
            int maxDefined = Enum.GetValues(typeof(MacroActionType)).Cast<int>().Max();
            MacroActionType outOfRange = (MacroActionType)(maxDefined + 1);

            // Act / Assert
            Assert.Throws<ArgumentException>(
                () => MacroActionFactory.Create(outOfRange),
                "An unknown enum value (beyond max) must throw ArgumentException.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // IMacroAction contract — applies to every action via EP "valid type"
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: an empty ActionId breaks tooltip lookups and the
        /// MacroActionCache key dictionary. Every concrete action must supply
        /// a non-whitespace ActionId or the UI silently shows a blank label.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void AllActions_HaveNonEmptyActionId()
        {
            // Arrange
            IEnumerable<MacroActionType> allTypes = Enum
                .GetValues(typeof(MacroActionType))
                .Cast<MacroActionType>()
                .Where(t => t != MacroActionType.None);

            foreach (MacroActionType type in allTypes)
            {
                // Act
                IMacroAction action = MacroActionFactory.Create(type);

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(action.ActionId),
                    $"{type} returned an action with a null/empty ActionId.");
            }
        }

        /// <summary>
        /// Risk mitigated: an empty DisplayName means the macro wheel button
        /// tooltip renders blank. Every action must have a human-readable label.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void AllActions_HaveNonEmptyDisplayName()
        {
            // Arrange
            IEnumerable<MacroActionType> allTypes = Enum
                .GetValues(typeof(MacroActionType))
                .Cast<MacroActionType>()
                .Where(t => t != MacroActionType.None);

            foreach (MacroActionType type in allTypes)
            {
                // Act
                IMacroAction action = MacroActionFactory.Create(type);

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(action.DisplayName),
                    $"{type} returned an action with a null/empty DisplayName.");
            }
        }

        /// <summary>
        /// Risk mitigated: duplicate ActionIds break any dictionary keyed on them
        /// (e.g. MacroActionCache, settings serialization). Every action must have
        /// a globally unique id.
        /// Test type: Contract
        /// </summary>
        [Test]
        [Category("Contract")]
        public void AllActions_HaveUniqueActionIds()
        {
            // Arrange
            IEnumerable<MacroActionType> allTypes = Enum
                .GetValues(typeof(MacroActionType))
                .Cast<MacroActionType>()
                .Where(t => t != MacroActionType.None);

            // Act
            List<string> ids = allTypes
                .Select(t => MacroActionFactory.Create(t).ActionId)
                .ToList();

            // Assert
            int distinctCount = ids.Distinct().Count();
            Assert.AreEqual(ids.Count, distinctCount,
                "Two or more actions share the same ActionId — IDs must be unique across all types.");
        }

        /// <summary>
        /// Risk mitigated: each call to Create() must return a fresh instance, not
        /// a shared singleton. Shared state between two buttons bound to the same
        /// action type could cause one button's Execute() to affect another's state.
        /// Test type: Unit
        /// </summary>
        [Test]
        [Category("Unit")]
        public void Create_ReturnsNewInstance_OnEachCall()
        {
            // Arrange
            const MacroActionType type = MacroActionType.Back;

            // Act
            IMacroAction first  = MacroActionFactory.Create(type);
            IMacroAction second = MacroActionFactory.Create(type);

            // Assert
            Assert.AreNotSame(first, second,
                "Create() must return a new instance on each call — shared state between buttons is not acceptable.");
        }
    }
}
