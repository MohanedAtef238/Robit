public class ZoomOutAction : KeyComboMacroAction
{
    public override string ActionId => "zoom_out";
    public override string DisplayName => "Zoom Out";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0xBD; // OEM_MINUS
}
