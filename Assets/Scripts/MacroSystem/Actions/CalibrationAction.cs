using UnityEngine;
using UnityEngine.Scripting;

public class CalibrationAction : IMacroAction
{
    [Preserve]
    static CalibrationAction() => MacroActionFactory.Register(MacroActionType.Calibration, () => new CalibrationAction());

    public string ActionId => "calibration";
    public string DisplayName => "Calibrate";

    public void Execute()
    {
        // TODO: wire into GazeCalibration when eye-tracking is connected
        RobitLogger.Log("[MacroButton] Executing: calibration");
    }
}

