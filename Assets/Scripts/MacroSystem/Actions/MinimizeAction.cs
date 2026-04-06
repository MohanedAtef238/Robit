using LnkParser.Constants;

public class MinimizeAction : KeyComboMacroAction
{
    public override string ActionId => "minimize";
    public override string DisplayName => "Minimize";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Down;
}
