using UnityEngine;
using UnityEngine.UIElements;

/// Wires the zoom-picker radio buttons (100 / 125 / 150 / 200 %) in
/// NewUXMLTemplate.uxml to Win32DisplayScaleInterop.
/// Attach this to the same GameObject as HomePageController.
public class ZoomPickerController : MonoBehaviour
{
    // Visual style for the currently selected button
    private static readonly Color ColActive   = new Color(0.62f, 0.25f, 1.00f, 0.95f); // vivid purple
    private static readonly Color ColInactive = new Color(0.49f, 0.40f, 0.71f, 0.65f); // muted purple

    private static readonly int[] Steps = { 100, 125, 150, 200 };
    private static readonly string[] BtnNames = { "zoom100Btn", "zoom125Btn", "zoom150Btn", "zoom200Btn" };

    private Button[] _buttons = new Button[4];
    private int      _currentPercent;
    private bool     _initialized;

    public void Initialize(VisualElement root)
    {
        for (int i = 0; i < Steps.Length; i++)
        {
            var btn = root.Q<Button>(BtnNames[i]);
            if (btn == null)
            {
                Debug.LogWarning($"[ZoomPickerController] Button {BtnNames[i]} not found.");
                continue;
            }
            _buttons[i] = btn;

            int captured = i;
            btn.clicked += () => OnZoomSelected(captured);
        }

        _currentPercent = Win32DisplayScaleInterop.GetScalePercent();
        RefreshHighlight();
        _initialized = true;
    }

    private void OnZoomSelected(int idx)
    {
        int percent = Steps[idx];
        if (percent == _currentPercent) return;

        _currentPercent = percent;
        RefreshHighlight();
        Win32DisplayScaleInterop.SetScalePercent(percent);
    }

    private void RefreshHighlight()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            bool active = Steps[i] == _currentPercent;
            _buttons[i].style.backgroundColor = active ? ColActive : ColInactive;
            // Slightly enlarge the active pill
            _buttons[i].style.scale = active
                ? new Scale(new Vector2(1.08f, 1.08f))
                : new Scale(Vector2.one);
        }
    }
}
