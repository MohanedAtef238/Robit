using LnkParser.Constants;

public class UndoAction : KeyComboMacroAction
{
    public override string ActionId => "undo";
    public override string DisplayName => "Undo";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.Z;
}

