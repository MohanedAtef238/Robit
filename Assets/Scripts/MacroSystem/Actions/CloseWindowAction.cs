using LnkParser.Constants;

public class CloseWindowAction : KeyComboMacroAction
{
    public override string ActionId => "close_window";
    public override string DisplayName => "Close";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.F4;
}
