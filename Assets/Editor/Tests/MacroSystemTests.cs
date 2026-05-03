using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace Robit.Tests
{
    public class MacroSystemTests
    {
        [SetUp]
        public void EnsureRegistrations()
        {
            // Force all static constructors before any test in this class runs
            var types = System.Reflection.Assembly.GetAssembly(typeof(BackAction))
                .GetTypes()
                .Where(t => typeof(IMacroAction).IsAssignableFrom(t) 
                            && !t.IsInterface && !t.IsAbstract);
            foreach (var t in types)
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
        }
        [Test]
        public void MacroActionFactory_ReturnsCorrectAction_ForUndo()
        {
            // 1. Arrange
            // We define the input type we want to test.
            MacroActionType inputType = MacroActionType.Undo;

            // 2. Act
            // We call the factory to create the action instance.
            IMacroAction action = MacroActionFactory.Create(inputType);

            // 3. Assert
            // We verify that the returned object is not null and is of the expected type.
            Assert.IsNotNull(action, "Factory should not return null for valid types.");
            Assert.IsTrue(action is UndoAction, $"Expected UndoAction, but got {action.GetType().Name}");
        }

        [Test]
        public void MacroActionFactory_ReturnsCorrectAction_ForRedo()
        {
            // 1. Arrange
            MacroActionType inputType = MacroActionType.Redo;

            // 2. Act
            IMacroAction action = MacroActionFactory.Create(inputType);

            // 3. Assert
            Assert.IsNotNull(action);
            Assert.IsTrue(action is RedoAction);
        }

        [Test]
        public void MacroActionFactory_ThrowsOnInvalidType()
        {
            // 1. Arrange
            // Cast an invalid integer to the enum to simulate a "future" or "corrupt" type.
            MacroActionType invalidType = (MacroActionType)9999;

            // 2. Act & 3. Assert
            // In NUnit, we can combine Act and Assert for exceptions.
            Assert.Throws<System.ArgumentException>(() => {
                MacroActionFactory.Create(invalidType);
            });
        }

        [Test]
        public void UndoAction_HasCorrectIdentity()
        {
            // 1. Arrange
            // We create an instance of the specific action.
            UndoAction action = new UndoAction();

            // 2. Act
            // We retrieve the properties we want to verify.
            string actionId = action.ActionId;
            string displayName = action.DisplayName;

            // 3. Assert
            // We verify the properties match the expected values.
            Assert.AreEqual("undo", actionId, "ActionId should be 'undo'");
            Assert.AreEqual("Undo", displayName, "DisplayName should be 'Undo'");
        }
    }
}

