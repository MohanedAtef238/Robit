using UnityEngine;
using UnityEngine.UIElements;

/// Wires the zoom-picker radio buttons (100 / 125 / 150 / 200 %) in
/// NewUXMLTemplate.uxml to Win32DisplayScaleInterop.
/// Attach this to the same GameObject as HomePageController.
public class ZoomPickerController : MonoBehaviour
{
    // CSS class used to mark the currently selected zoom button.
    private const string SelectedClass = "zoom-selected";

    private static readonly int[] Steps = { 100, 125, 150, 200 };
    private static readonly string[] BtnNames = { "zoom100Btn", "zoom125Btn", "zoom150Btn", "zoom200Btn" };

    private Button[] _buttons = new Button[4];
    private int      _currentPercent;
    private bool     _initialized;

    public void Initialize(VisualElement root)
    {
        if (_initialized) return;

        int foundCount = 0;
        for (int i = 0; i < Steps.Length; i++)
        {
            var btn = root.Q<Button>(BtnNames[i]);
            if (btn == null)
            {
                RobitLogger.LogWarning($"[ZoomPickerController] Button {BtnNames[i]} not found.");
                continue;
            }
            _buttons[i] = btn;
            // Ensure USS controls the button background (clear any inline color).
            btn.style.backgroundColor = StyleKeyword.Null;
            foundCount++;

            int captured = i;
            btn.clicked += () => OnZoomSelected(captured);
        }

        if (foundCount == 0)
            RobitLogger.LogError("[ZoomPickerController] No zoom buttons were found in the active UI document.");

        _currentPercent = Win32DisplayScaleInterop.GetScalePercent();
        RefreshHighlight();
        _initialized = true;
    }

    private void OnZoomSelected(int idx)
    {
        int percent = Steps[idx];
        if (percent == _currentPercent) return;

        bool applied = Win32DisplayScaleInterop.SetScalePercent(percent);
        _currentPercent = applied ? percent : Win32DisplayScaleInterop.GetScalePercent();
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            bool active = Steps[i] == _currentPercent;
            if (active)
                _buttons[i].AddToClassList(SelectedClass);
            else
                _buttons[i].RemoveFromClassList(SelectedClass);
        }
    }
}

