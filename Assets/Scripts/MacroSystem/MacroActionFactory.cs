using System;
using System.Collections.Generic;

/// <summary>
/// Factory that creates the correct IMacroAction instance from a MacroActionType enum value.
/// Uses a self-registration pattern to keep cyclomatic complexity low (constant 3).
/// </summary>
public static class MacroActionFactory
{
    private static readonly Dictionary<MacroActionType, Func<IMacroAction>> _registry = new();

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

