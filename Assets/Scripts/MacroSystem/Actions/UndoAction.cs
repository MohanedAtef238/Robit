public class UndoAction : KeyComboMacroAction
{
    public override string ActionId => "undo";
    public override string DisplayName => "Undo";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x5A; // Z
}
