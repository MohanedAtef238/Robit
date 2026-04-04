public class RedoAction : KeyComboMacroAction
{
    public override string ActionId => "redo";
    public override string DisplayName => "Redo";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x59; // Y
}
