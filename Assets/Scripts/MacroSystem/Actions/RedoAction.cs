using LnkParser.Constants;

public class RedoAction : KeyComboMacroAction
{
    public override string ActionId => "redo";
    public override string DisplayName => "Redo";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.Y;
}
