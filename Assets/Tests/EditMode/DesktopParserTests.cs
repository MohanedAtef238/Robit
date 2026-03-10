using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DesktopParserTests
{
    private GameObject _go;
    private object _parser;
    private Type _parserType;

    [SetUp]
    public void SetUp()
    {
        _parserType = FindType("DesktopParser");
        _go = new GameObject("DesktopParserTests_GO");
        _parser = _go.AddComponent(_parserType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        // Clear static icon cache to keep tests isolated.
        var cacheField = _parserType.GetField("IconTextureCache", BindingFlags.NonPublic | BindingFlags.Static);
        if (cacheField?.GetValue(null) is System.Collections.IDictionary dict)
        {
            dict.Clear();
        }
    }

    [Test]
    public void GetIconUrl_ReturnsExpectedUrl_ForExactShortcutMatch()
    {
        var url = (string)InvokePrivate("GetIconUrl", "chrome", "something");
        Assert.That(url, Is.EqualTo("https://img.icons8.com/color/96/chrome--v1.png"));
    }

    [Test]
    public void GetIconUrl_ReturnsExpectedUrl_ForExeFallbackMatch()
    {
        var url = (string)InvokePrivate("GetIconUrl", "some app name", "msedge");
        Assert.That(url, Is.EqualTo("https://img.icons8.com/color/96/ms-edge-new.png"));
    }

    [Test]
    public void GetIconUrl_ReturnsExpectedUrl_ForPartialNameMatch()
    {
        var url = (string)InvokePrivate("GetIconUrl", "Discord Canary", "randomexe");
        Assert.That(url, Is.EqualTo("https://img.icons8.com/color/96/discord-logo.png"));
    }

    [Test]
    public void GetIconUrl_ReturnsNull_WhenNoMappingExists()
    {
        var url = (string)InvokePrivate("GetIconUrl", "not-mapped", "still-not-mapped");
        Assert.That(url, Is.Null);
    }

    [Test]
    public void IsAllowedApp_ReturnsTrue_ForExactAndPartialMatches()
    {
        var exactShortcut = (bool)InvokePrivate("IsAllowedApp", "chrome", "anything");
        var exactExe = (bool)InvokePrivate("IsAllowedApp", "random", "telegram");
        var partial = (bool)InvokePrivate("IsAllowedApp", "My Discord App", "random");

        Assert.That(exactShortcut, Is.True);
        Assert.That(exactExe, Is.True);
        Assert.That(partial, Is.True);
    }

    [Test]
    public void IsAllowedApp_ReturnsFalse_ForUnknownApp()
    {
        var allowed = (bool)InvokePrivate("IsAllowedApp", "unknown_app", "unknown_exe");
        Assert.That(allowed, Is.False);
    }

    [Test]
    public void AddShortcut_AppendsEntry_WithExpectedFields()
    {
        var icon = new Texture2D(2, 2);
        InvokePrivate("AddShortcut", "Discord", @"C:\Apps\Discord\Discord.exe", icon);

        var shortcutsField = _parserType.GetField("shortcuts", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(shortcutsField, Is.Not.Null);
        var list = shortcutsField.GetValue(_parser) as IList;
        Assert.That(list, Is.Not.Null);
        Assert.That(list.Count, Is.EqualTo(1));

        var item = list[0];
        var itemType = item.GetType();

        var name = itemType.GetField("Name").GetValue(item) as string;
        var targetPath = itemType.GetField("TargetPath").GetValue(item) as string;
        var workingDirectory = itemType.GetField("WorkingDirectory").GetValue(item) as string;
        var storedIcon = itemType.GetField("Icon").GetValue(item) as Texture2D;

        Assert.That(name, Is.EqualTo("Discord"));
        Assert.That(targetPath, Is.EqualTo(@"C:\Apps\Discord\Discord.exe"));
        Assert.That(workingDirectory, Is.EqualTo(string.Empty));
        Assert.That(storedIcon, Is.SameAs(icon));

        UnityEngine.Object.DestroyImmediate(icon);
    }

    [Test]
    public void TryGetCachedIcon_ReturnsTrue_AfterCacheIcon()
    {
        var icon = new Texture2D(4, 4);
        const string url = "https://img.icons8.com/color/96/discord-logo.png";

        InvokePrivateStatic("CacheIcon", url, icon);
        var args = new object[] { url, null };
        var hit = (bool)InvokePrivateStaticWithRefOut("TryGetCachedIcon", args);

        Assert.That(hit, Is.True);
        Assert.That(args[1], Is.SameAs(icon));

        UnityEngine.Object.DestroyImmediate(icon);
    }

    [Test]
    public void FetchIconAndAddShortcut_UsesCache_AndCompletesWithoutYield()
    {
        var icon = new Texture2D(8, 8);
        const string url = "https://img.icons8.com/color/96/chrome--v1.png";
        InvokePrivateStatic("CacheIcon", url, icon);

        var routine = (System.Collections.IEnumerator)InvokePrivate("FetchIconAndAddShortcut", "Chrome", @"C:\Apps\Chrome\chrome.exe", url);
        var moved = routine.MoveNext();

        // On cache hit, coroutine should exit before issuing any web request.
        Assert.That(moved, Is.False);

        var shortcutsField = _parserType.GetField("shortcuts", BindingFlags.Public | BindingFlags.Instance);
        var list = shortcutsField.GetValue(_parser) as System.Collections.IList;
        Assert.That(list.Count, Is.EqualTo(1));

        var item = list[0];
        var itemType = item.GetType();
        var storedIcon = itemType.GetField("Icon").GetValue(item) as Texture2D;
        Assert.That(storedIcon, Is.SameAs(icon));

        UnityEngine.Object.DestroyImmediate(icon);
    }

    private object InvokePrivate(string methodName, params object[] args)
    {
        var method = _parserType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found.");
        return method.Invoke(_parser, args);
    }

    private object InvokePrivateStatic(string methodName, params object[] args)
    {
        var method = _parserType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Static method '{methodName}' was not found.");
        return method.Invoke(null, args);
    }

    private object InvokePrivateStaticWithRefOut(string methodName, object[] args)
    {
        var method = _parserType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Static method '{methodName}' was not found.");
        return method.Invoke(null, args);
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
