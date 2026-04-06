using LnkParser.Constants;

public class NewTabAction : KeyComboMacroAction
{
    public override string ActionId => "new_tab";
    public override string DisplayName => "New Tab";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.T;
}
