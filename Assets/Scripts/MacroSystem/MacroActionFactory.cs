/// Factory that creates the correct IMacroAction instance from a MacroActionType enum value.
public static class MacroActionFactory
{
    public static IMacroAction Create(MacroActionType type)
    {
        return type switch
        {
            MacroActionType.None            => null,
            MacroActionType.Back            => new BackAction(),
            MacroActionType.Forward         => new ForwardAction(),
            MacroActionType.Refresh         => new RefreshAction(),
            MacroActionType.NewTab          => new NewTabAction(),
            MacroActionType.CloseTab        => new CloseTabAction(),
            MacroActionType.SwitchWindow    => new SwitchWindowAction(),
            MacroActionType.ZoomIn          => new ZoomInAction(),
            MacroActionType.ZoomOut         => new ZoomOutAction(),
            MacroActionType.Screenshot      => new ScreenshotAction(),
            MacroActionType.PageUp          => new PageUpAction(),
            MacroActionType.PageDown        => new PageDownAction(),
            MacroActionType.ReturnToDesktop => new ReturnToDesktopAction(),
            MacroActionType.SnapLeft        => new SnapLeftAction(),
            MacroActionType.SnapRight       => new SnapRightAction(),
            MacroActionType.MaximizeRestore => new MaximizeRestoreAction(),
            MacroActionType.Minimize        => new MinimizeAction(),
            MacroActionType.CloseWindow     => new CloseWindowAction(),
            MacroActionType.Undo            => new UndoAction(),
            MacroActionType.Redo            => new RedoAction(),
            MacroActionType.MuteToggle      => new MuteToggleAction(),
            MacroActionType.FindOnPage      => new FindOnPageAction(),
            MacroActionType.LockScreen      => new LockScreenAction(),
            MacroActionType.HomeDashboard   => new HomePageAction(),
            MacroActionType.AppCycler       => new AppCyclerAction(),
            MacroActionType.Settings        => new SettingsAction(),
            MacroActionType.Calibration     => new CalibrationAction(),
            _ => throw new System.ArgumentException($"Unknown MacroActionType: {type}")
        };
    }
}
