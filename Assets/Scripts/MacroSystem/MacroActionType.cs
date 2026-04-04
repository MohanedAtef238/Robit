/// Available macro action types. Add new entries here when creating new actions.
public enum MacroActionType
{
    None,
    // Navigate
    Back,
    Forward,
    Refresh,
    // Tabs
    NewTab,
    CloseTab,
    SwitchWindow,
    // Read
    ZoomIn,
    ZoomOut,
    Screenshot,
    // Scroll
    PageUp,
    PageDown,
    ReturnToDesktop,
    // Snap
    SnapLeft,
    SnapRight,
    MaximizeRestore,
    // Window
    Minimize,
    CloseWindow,
    Undo,
    // Edit
    Redo,
    MuteToggle,
    FindOnPage,
    // Unused (kept for compatibility)
    LockScreen,
    // System
    HomeDashboard,
    AppCycler,
    Settings,
    // Legacy (kept for compatibility)
    Calibration,
}
