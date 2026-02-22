using UnityEngine.UIElements;

[UxmlElement]
public partial class MacroButton : Button
{
    public IMacroAction Action { get; private set; }
    public IInputProvider InputProvider { get; private set; }

    public MacroButton() : base() { }

    /// Bind an action and input provider to this button.
    public void Bind(IMacroAction action, IInputProvider inputProvider)
    {
        Action = action;
        InputProvider = inputProvider;
        InputProvider.Attach(this, () => Action.Execute());
    }

    /// Clean up all listeners.
    public void Unbind()
    {
        if (InputProvider != null)
            InputProvider.Detach(this);
    }
}
