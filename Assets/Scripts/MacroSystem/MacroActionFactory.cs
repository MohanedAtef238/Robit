/// Factory that creates the correct IMacroAction instance from a MacroActionType enum value.
public static class MacroActionFactory
{
    public static IMacroAction Create(MacroActionType type)
    {
        return type switch
        {
            MacroActionType.ZoomIn       => new ZoomInAction(),
            MacroActionType.ZoomOut      => new ZoomOutAction(),
            MacroActionType.SwitchWindow => new SwitchWindowAction(),
            MacroActionType.Calibration  => new CalibrationAction(),
            MacroActionType.Home         => new HomeMacroAction(),
            _ => throw new System.ArgumentException($"Unknown MacroActionType: {type}")
        };
    }
}
