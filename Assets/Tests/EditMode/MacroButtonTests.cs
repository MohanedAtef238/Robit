using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.UIElements;

public class MacroButtonTests
{
    [Test]
    public void Bind_SetsActionAndInputProviderProperties()
    {
        var macroButtonType = FindType("MacroButton");
        var actionType = FindType("CalibrationAction");
        var inputProviderType = FindType("PointerInputProvider");

        var button = Activator.CreateInstance(macroButtonType);
        var action = Activator.CreateInstance(actionType);
        var provider = Activator.CreateInstance(inputProviderType);

        var bindMethod = macroButtonType.GetMethod("Bind", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(bindMethod, Is.Not.Null);

        bindMethod.Invoke(button, new[] { action, provider });

        var actionProp = macroButtonType.GetProperty("Action", BindingFlags.Public | BindingFlags.Instance);
        var providerProp = macroButtonType.GetProperty("InputProvider", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(actionProp, Is.Not.Null);
        Assert.That(providerProp, Is.Not.Null);

        var actualAction = actionProp.GetValue(button);
        var actualProvider = providerProp.GetValue(button);

        Assert.That(actualAction, Is.Not.Null);
        Assert.That(actualProvider, Is.Not.Null);
        Assert.That(actualAction.GetType().Name, Is.EqualTo("CalibrationAction"));
        Assert.That(actualProvider.GetType().Name, Is.EqualTo("PointerInputProvider"));
    }

    [Test]
    public void Unbind_DoesNotThrow_AfterBind()
    {
        var macroButtonType = FindType("MacroButton");
        var actionType = FindType("CalibrationAction");
        var inputProviderType = FindType("PointerInputProvider");

        var button = Activator.CreateInstance(macroButtonType);
        var action = Activator.CreateInstance(actionType);
        var provider = Activator.CreateInstance(inputProviderType);

        macroButtonType.GetMethod("Bind", BindingFlags.Public | BindingFlags.Instance).Invoke(button, new[] { action, provider });

        var unbindMethod = macroButtonType.GetMethod("Unbind", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(unbindMethod, Is.Not.Null);
        Assert.DoesNotThrow(() => unbindMethod.Invoke(button, null));
    }

    [Test]
    public void PointerInputProvider_AttachAndDetach_UpdatesInternalCallbackMap()
    {
        var inputProviderType = FindType("PointerInputProvider");
        var provider = Activator.CreateInstance(inputProviderType);
        var element = new VisualElement();

        var attachMethod = inputProviderType.GetMethod("Attach", BindingFlags.Public | BindingFlags.Instance);
        var detachMethod = inputProviderType.GetMethod("Detach", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(attachMethod, Is.Not.Null);
        Assert.That(detachMethod, Is.Not.Null);

        attachMethod.Invoke(provider, new object[] { element, (Action)(() => { }) });

        var callbacksField = inputProviderType.GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(callbacksField, Is.Not.Null);
        var callbacks = callbacksField.GetValue(provider) as IDictionary;
        Assert.That(callbacks, Is.Not.Null);
        Assert.That(callbacks.Count, Is.EqualTo(1));

        detachMethod.Invoke(provider, new object[] { element });
        Assert.That(callbacks.Count, Is.EqualTo(0));
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
