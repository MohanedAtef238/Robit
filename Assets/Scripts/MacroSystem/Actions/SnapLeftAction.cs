using LnkParser.Constants;

public class SnapLeftAction : KeyComboMacroAction
{
    public override string ActionId => "snap_left";
    public override string DisplayName => "Snap Left";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Left;
}
