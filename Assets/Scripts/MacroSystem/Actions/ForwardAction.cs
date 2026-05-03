using LnkParser.Constants;

public class ForwardAction : KeyComboMacroAction
{
    public override string ActionId => "forward";
    public override string DisplayName => "Forward";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Right;
}

