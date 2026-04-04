public class NewTabAction : KeyComboMacroAction
{
    public override string ActionId => "new_tab";
    public override string DisplayName => "New Tab";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x54; // T
}
