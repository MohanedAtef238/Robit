public class SnapLeftAction : KeyComboMacroAction
{
    public override string ActionId => "snap_left";
    public override string DisplayName => "Snap Left";
    protected override byte[] Modifiers => new byte[] { VK_LWIN };
    protected override byte MainKey => 0x25; // Left
}
