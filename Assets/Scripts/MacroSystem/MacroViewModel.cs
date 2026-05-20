using System;
using System.Collections.Generic;

[System.Serializable]
public struct MacroGroup
{
    public string name;
    public MacroActionType action0;
    public MacroActionType action1;
    public MacroActionType action2;
}

public class MacroViewModel
{
    public static readonly MacroGroup[] Groups = new[]
    {
        new MacroGroup { name = "System",    action0 = MacroActionType.HomeDashboard, action1 = MacroActionType.AppCycler,     action2 = MacroActionType.Settings },
        new MacroGroup { name = "Navigate",  action0 = MacroActionType.Back,          action1 = MacroActionType.Forward,       action2 = MacroActionType.Refresh },
        new MacroGroup { name = "Tabs",      action0 = MacroActionType.NewTab,        action1 = MacroActionType.CloseTab,      action2 = MacroActionType.None },
        new MacroGroup { name = "Read",      action0 = MacroActionType.ZoomIn,        action1 = MacroActionType.ZoomOut,       action2 = MacroActionType.Screenshot },
        new MacroGroup { name = "Scroll",    action0 = MacroActionType.PageUp,        action1 = MacroActionType.PageDown,      action2 = MacroActionType.None },
        new MacroGroup { name = "Snap",      action0 = MacroActionType.SnapLeft,      action1 = MacroActionType.SnapRight,     action2 = MacroActionType.MaximizeRestore },
        new MacroGroup { name = "Window",    action0 = MacroActionType.Minimize,      action1 = MacroActionType.CloseWindow,   action2 = MacroActionType.None },
        new MacroGroup { name = "Edit",      action0 = MacroActionType.Undo,          action1 = MacroActionType.Redo,          action2 = MacroActionType.None },
        new MacroGroup { name = "Tools",     action0 = MacroActionType.MuteToggle,    action1 = MacroActionType.FindOnPage,    action2 = MacroActionType.None },
    };

    public int CurrentGroupIndex { get; private set; }
    public bool IsOpen { get; private set; }

    public event Action<int> OnGroupChanged;
    public event Action<bool> OnMenuToggled;

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        CurrentGroupIndex = 0; // Reset on open
        OnMenuToggled?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        OnMenuToggled?.Invoke(false);
    }

    public void NextGroup()
    {
        if (!IsOpen || Groups.Length <= 1) return;
        CurrentGroupIndex = (CurrentGroupIndex + 1) % Groups.Length;
        OnGroupChanged?.Invoke(CurrentGroupIndex);
    }

    public void PrevGroup()
    {
        if (!IsOpen || Groups.Length <= 1) return;
        CurrentGroupIndex = (CurrentGroupIndex - 1 + Groups.Length) % Groups.Length;
        OnGroupChanged?.Invoke(CurrentGroupIndex);
    }

    public MacroGroup GetCurrentGroup()
    {
        return Groups[CurrentGroupIndex];
    }
}
