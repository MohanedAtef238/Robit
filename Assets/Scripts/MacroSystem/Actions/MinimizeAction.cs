public class MinimizeAction : KeyComboMacroAction
{
    public override string ActionId => "minimize";
    public override string DisplayName => "Minimize";
    protected override byte[] Modifiers => new byte[] { VK_LWIN };
    protected override byte MainKey => 0x28; // Down
}
