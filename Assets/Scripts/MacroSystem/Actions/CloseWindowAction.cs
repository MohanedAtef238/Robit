public class CloseWindowAction : KeyComboMacroAction
{
    public override string ActionId => "close_window";
    public override string DisplayName => "Close";
    protected override byte[] Modifiers => new byte[] { VK_MENU };
    protected override byte MainKey => 0x73; // F4
}
