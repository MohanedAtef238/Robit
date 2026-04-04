public class CloseTabAction : KeyComboMacroAction
{
    public override string ActionId => "close_tab";
    public override string DisplayName => "Close Tab";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x57; // W
}
