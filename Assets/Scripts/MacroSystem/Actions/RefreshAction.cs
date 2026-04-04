public class RefreshAction : KeyComboMacroAction
{
    public override string ActionId => "refresh";
    public override string DisplayName => "Refresh";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x52; // R
}
