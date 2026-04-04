public class LockScreenAction : KeyComboMacroAction
{
    public override string ActionId => "lock_screen";
    public override string DisplayName => "Lock";
    protected override byte[] Modifiers => new byte[] { VK_LWIN };
    protected override byte MainKey => 0x4C; // L
    protected override bool FocusBehind => false;
}
