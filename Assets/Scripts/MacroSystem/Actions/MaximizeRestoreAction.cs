using LnkParser.Constants;

public class MaximizeRestoreAction : KeyComboMacroAction
{
    public override string ActionId => "maximize_restore";
    public override string DisplayName => "Maximize";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Up;
}
