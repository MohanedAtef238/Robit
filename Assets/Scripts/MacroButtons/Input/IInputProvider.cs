using System;
using UnityEngine.UIElements;

/// Abstraction for how a macro button detects an "activation" (click, gaze dwell, etc.).
/// Implement this interface to add new input methods without changing button or action code.
public interface IInputProvider
{
    void Attach(VisualElement target, Action onActivated);

    /// Remove all listeners this provider added to the target element.
    void Detach(VisualElement target);
}
