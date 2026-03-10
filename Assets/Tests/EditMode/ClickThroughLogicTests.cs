using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickThroughLogicTests
{
    private Type _transparencyType;
    private Type _uiClickHandlerType;

    [SetUp]
    public void SetUp()
    {
        _transparencyType = FindType("Transparency");
        _uiClickHandlerType = FindType("UIClickHandler");
    }

    [Test]
    public void SetClickThrough_TracksLastRequestedState_AndCallCount()
    {
        var go = new GameObject("Transparency_Test");
        try
        {
            var transparency = go.AddComponent(_transparencyType);
            InvokeInstance(transparency, "SetClickThrough", false);
            InvokeInstance(transparency, "SetClickThrough", true);

            var lastRequested = (bool)GetProperty(transparency, "LastRequestedClickThrough");
            var requestCount = (int)GetProperty(transparency, "ClickThroughRequestCount");

            Assert.That(lastRequested, Is.True);
            Assert.That(requestCount, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OnPointerEnter_RequestsNonClickThrough()
    {
        var handlerGo = new GameObject("UIClickHandler_Test");
        var transparencyGo = new GameObject("Transparency_Test");
        try
        {
            var handler = handlerGo.AddComponent(_uiClickHandlerType);
            var transparency = transparencyGo.AddComponent(_transparencyType);
            SetPrivateField(handler, "transparency", transparency);

            InvokeInstance(handler, "OnPointerEnter", new PointerEventData(null));

            var lastRequested = (bool)GetProperty(transparency, "LastRequestedClickThrough");
            var requestCount = (int)GetProperty(transparency, "ClickThroughRequestCount");
            Assert.That(lastRequested, Is.False);
            Assert.That(requestCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(handlerGo);
            UnityEngine.Object.DestroyImmediate(transparencyGo);
        }
    }

    [Test]
    public void OnPointerExit_RequestsClickThrough_WhenTransparencyEnabled()
    {
        var handlerGo = new GameObject("UIClickHandler_Test");
        var transparencyGo = new GameObject("Transparency_Test");
        try
        {
            var handler = handlerGo.AddComponent(_uiClickHandlerType);
            var transparency = transparencyGo.AddComponent(_transparencyType);
            SetPrivateField(handler, "transparency", transparency);

            InvokeInstance(transparency, "SetClickThrough", false);
            InvokeInstance(handler, "OnPointerExit", new PointerEventData(null));

            var lastRequested = (bool)GetProperty(transparency, "LastRequestedClickThrough");
            var requestCount = (int)GetProperty(transparency, "ClickThroughRequestCount");
            Assert.That(lastRequested, Is.True);
            Assert.That(requestCount, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(handlerGo);
            UnityEngine.Object.DestroyImmediate(transparencyGo);
        }
    }

    [Test]
    public void OnPointerExit_DoesNothing_WhenTransparencyComponentDisabled()
    {
        var handlerGo = new GameObject("UIClickHandler_Test");
        var transparencyGo = new GameObject("Transparency_Test");
        try
        {
            var handler = handlerGo.AddComponent(_uiClickHandlerType);
            var transparency = transparencyGo.AddComponent(_transparencyType);
            SetPrivateField(handler, "transparency", transparency);

            InvokeInstance(transparency, "SetClickThrough", false);
            ((Behaviour)transparency).enabled = false;
            InvokeInstance(handler, "OnPointerExit", new PointerEventData(null));

            var lastRequested = (bool)GetProperty(transparency, "LastRequestedClickThrough");
            var requestCount = (int)GetProperty(transparency, "ClickThroughRequestCount");
            Assert.That(lastRequested, Is.False);
            Assert.That(requestCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(handlerGo);
            UnityEngine.Object.DestroyImmediate(transparencyGo);
        }
    }

    private static Type FindType(string typeName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == typeName);
        Assert.That(type, Is.Not.Null, $"Type '{typeName}' was not found.");
        return type;
    }

    private static void InvokeInstance(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on '{target.GetType().Name}'.");
        method.Invoke(target, args);
    }

    private static object GetProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"Property '{propertyName}' not found on '{target.GetType().Name}'.");
        return property.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }
}
