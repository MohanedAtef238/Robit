using LnkParser.Constants;

public class SwitchWindowAction : KeyComboMacroAction
{
    public override string ActionId => "switch_window";
    public override string DisplayName => "Switch Window";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Tab;
}

