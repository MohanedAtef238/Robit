using LnkParser.Constants;

public class SnapRightAction : KeyComboMacroAction
{
    public override string ActionId => "snap_right";
    public override string DisplayName => "Snap Right";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Right;
}
