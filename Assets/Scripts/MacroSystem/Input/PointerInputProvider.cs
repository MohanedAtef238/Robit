using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// Default input provider — listens to PointerUpEvent on UI Toolkit elements.
/// Covers mouse clicks, touch, pen, and any pointer device routed through the Input System
/// (including eye-gaze cursors that emit pointer events).
public class PointerInputProvider : IInputProvider
{
    // Store callbacks so we can unregister them in Detach()
    private readonly Dictionary<VisualElement, EventCallback<PointerUpEvent>> _callbacks = new();

    public void Attach(VisualElement target, Action onActivated)
    {
        EventCallback<PointerUpEvent> callback = evt => onActivated?.Invoke();
        target.RegisterCallback(callback);
        _callbacks[target] = callback;
    }

    public void Detach(VisualElement target)
    {
        if (_callbacks.TryGetValue(target, out var callback))
        {
            target.UnregisterCallback(callback);
            _callbacks.Remove(target);
        }
    }
}
