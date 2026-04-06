using LnkParser.Constants;

public class BackAction : KeyComboMacroAction
{
    public override string ActionId => "back";
    public override string DisplayName => "Back";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Left;
}
