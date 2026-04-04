public class SnapRightAction : KeyComboMacroAction
{
    public override string ActionId => "snap_right";
    public override string DisplayName => "Snap Right";
    protected override byte[] Modifiers => new byte[] { VK_LWIN };
    protected override byte MainKey => 0x27; // Right
}
