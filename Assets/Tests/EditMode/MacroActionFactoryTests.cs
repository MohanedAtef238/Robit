using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MacroActionFactoryTests
{
    [Test]
    public void Create_ReturnsExpectedConcreteAction_ForEachEnumValue()
    {
        var factoryType = FindType("MacroActionFactory");
        var actionTypeEnum = FindType("MacroActionType");
        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.That(createMethod, Is.Not.Null);

        AssertAction(createMethod, actionTypeEnum, "ZoomIn", "ZoomInAction");
        AssertAction(createMethod, actionTypeEnum, "ZoomOut", "ZoomOutAction");
        AssertAction(createMethod, actionTypeEnum, "SwitchWindow", "SwitchWindowAction");
        AssertAction(createMethod, actionTypeEnum, "Calibration", "CalibrationAction");
        AssertAction(createMethod, actionTypeEnum, "Home", "HomeMacroAction");
    }

    [Test]
    public void Create_ThrowsArgumentException_ForUnknownEnumValue()
    {
        var factoryType = FindType("MacroActionFactory");
        var actionTypeEnum = FindType("MacroActionType");
        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.That(createMethod, Is.Not.Null);

        var unknownValue = Enum.ToObject(actionTypeEnum, 999);
        var ex = Assert.Throws<TargetInvocationException>(() => createMethod.Invoke(null, new[] { unknownValue }));
        Assert.That(ex.InnerException, Is.TypeOf<ArgumentException>());
    }

    private static void AssertAction(MethodInfo createMethod, Type actionTypeEnum, string enumName, string expectedTypeName)
    {
        var enumValue = Enum.Parse(actionTypeEnum, enumName);
        var action = createMethod.Invoke(null, new[] { enumValue });

        Assert.That(action, Is.Not.Null);
        Assert.That(action.GetType().Name, Is.EqualTo(expectedTypeName));
    }

    private static Type FindType(string name)
    {
        var type = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == name);

        Assert.That(type, Is.Not.Null, $"Type '{name}' was not found in loaded assemblies.");
        return type;
    }
}
