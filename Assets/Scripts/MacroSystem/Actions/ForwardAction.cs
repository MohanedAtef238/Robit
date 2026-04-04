public class ForwardAction : KeyComboMacroAction
{
    public override string ActionId => "forward";
    public override string DisplayName => "Forward";
    protected override byte[] Modifiers => new byte[] { VK_MENU };
    protected override byte MainKey => 0x27; // Right
}
