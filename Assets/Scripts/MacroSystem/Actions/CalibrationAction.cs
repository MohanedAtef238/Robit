using UnityEngine;

public class CalibrationAction : IMacroAction
{
    public string ActionId => "calibration";
    public string DisplayName => "Calibrate";

    public void Execute()
    {
        // TODO: wire into GazeCalibration when eye-tracking is connected
        RobitLogger.Log("[MacroButton] Executing: calibration");
    }
}

