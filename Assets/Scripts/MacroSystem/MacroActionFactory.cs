using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

/// <summary>
/// Factory that creates the correct IMacroAction instance from a MacroActionType enum value.
/// Uses a self-registration pattern to keep cyclomatic complexity low (constant 3).
/// </summary>
public static class MacroActionFactory
{
    private static readonly Dictionary<MacroActionType, Func<IMacroAction>> _registry = new();

    /// <summary>
    /// Forces every action's static constructor to run before any scene loads.
    /// Required because C# static constructors are lazy — they only execute when a type
    /// is first referenced. The self-registration pattern depends on them running, but
    /// nothing in the new code directly instantiates or references the action types,
    /// so without this bootstrapper the registry stays empty and Create() always throws.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    [Preserve]
    private static void Bootstrap()
    {
        RuntimeHelpers.RunClassConstructor(typeof(AppCyclerAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(BackAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CalibrationAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CloseTabAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CloseWindowAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(FindOnPageAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ForwardAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(HomePageAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(LockScreenAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(MaximizeRestoreAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(MinimizeAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(MuteToggleAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(NewTabAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(PageDownAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(PageUpAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RedoAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RefreshAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ReturnToDesktopAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ScreenshotAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SettingsAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SnapLeftAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SnapRightAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SwitchWindowAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(UndoAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ZoomInAction).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ZoomOutAction).TypeHandle);
    }

    /// <summary>
    /// Registers a factory function for a specific MacroActionType.
    /// Called by individual Action classes in their static constructors.
    /// </summary>
    public static void Register(MacroActionType type, Func<IMacroAction> factory)
    {
        _registry[type] = factory;
    }

    /// <summary>
    /// Creates an instance of the requested action type.
    /// Complexity: 3 (null check, TryGetValue, throw).
    /// </summary>
    public static IMacroAction Create(MacroActionType type)
    {
        if (type == MacroActionType.None)
            return null;

        if (_registry.TryGetValue(type, out var factory))
            return factory();

        throw new ArgumentException($"No factory registered for {type}. Did you forget the static constructor or [Preserve] attribute?");
    }
}

