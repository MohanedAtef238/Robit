public class SwitchWindowAction : KeyComboMacroAction
{
    public override string ActionId => "switch_window";
    public override string DisplayName => "Switch Window";
    protected override byte[] Modifiers => new byte[] { VK_MENU };
    protected override byte MainKey => 0x09; // Tab
}
