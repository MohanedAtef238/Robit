using UnityEngine;

// Simple script to test EyeTracking and Calibration triggers
public class EyeTrackingTester : MonoBehaviour
{
    void Update()
    {
        // Check if keyboard is present
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        // Press C to start calibration
        if (UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (CalibrationManager.Instance != null)
            {
                Debug.Log("[Tester] Starting Calibration...");
                CalibrationManager.Instance.StartCalibration();
            }
            else
            {
                Debug.LogError("[Tester] CalibrationManager.Instance is null! Is it in the scene?");
            }
        }

        // Press S to stop/cancel calibration
        if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
        {
            if (CalibrationManager.Instance != null)
            {
                CalibrationManager.Instance.CancelCalibration();
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("EYE TRACKING TEST INTERFACE");
        
        if (EyeTrackingInput.Instance != null)
        {
            string color = EyeTrackingInput.Instance.IsConnected ? "green" : "red";
            GUILayout.Label($"Status: <color={color}>{(EyeTrackingInput.Instance.IsConnected ? "Connected" : "Disconnected")}</color>");
            GUILayout.Label($"Gaze Pos: {EyeTrackingInput.Instance.GazePosition}");
            GUILayout.Label($"Blink: {(EyeTrackingInput.Instance.IsBlinking ? "YES" : "NO")}");
        }

        GUILayout.Space(10);
        GUILayout.Label("CONTROLS:");
        GUILayout.Label("- Press 'C' to Start Calibration");
        GUILayout.Label("- Press 'S' to Stop Calibration");
        
        if (CalibrationManager.Instance != null && CalibrationManager.Instance.IsCalibrating)
        {
            GUILayout.Label("<color=yellow>CALIBRATING...</color>");
        }
        
        GUILayout.EndArea();
    }
}
