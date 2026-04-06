using LnkParser.Constants;

public class CloseTabAction : KeyComboMacroAction
{
    public override string ActionId => "close_tab";
    public override string DisplayName => "Close Tab";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.W;
}
