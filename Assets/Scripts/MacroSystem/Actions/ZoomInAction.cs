public class ZoomInAction : KeyComboMacroAction
{
    public override string ActionId => "zoom_in";
    public override string DisplayName => "Zoom In";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0xBB; // OEM_PLUS
}
