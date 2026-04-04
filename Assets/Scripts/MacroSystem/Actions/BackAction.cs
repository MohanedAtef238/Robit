public class BackAction : KeyComboMacroAction
{
    public override string ActionId => "back";
    public override string DisplayName => "Back";
    protected override byte[] Modifiers => new byte[] { VK_MENU };
    protected override byte MainKey => 0x25; // Left
}
