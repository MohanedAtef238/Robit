/// Interface for all macro button actions.
public interface IMacroAction
{
    /// Unique identifier for this action (like "zoom_in", "switch_window").
    string ActionId { get; }

    /// Human-readable display name or the name that exists in UI labels or tooltips.
    string DisplayName { get; }

    /// this is the action thats going to be executed when the button is pressed, overriden for each new macro
    void Execute();
}
