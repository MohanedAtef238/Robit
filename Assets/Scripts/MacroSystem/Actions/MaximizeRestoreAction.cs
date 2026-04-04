public class MaximizeRestoreAction : KeyComboMacroAction
{
    public override string ActionId => "maximize_restore";
    public override string DisplayName => "Maximize";
    protected override byte[] Modifiers => new byte[] { VK_LWIN };
    protected override byte MainKey => 0x26; // Up
}
