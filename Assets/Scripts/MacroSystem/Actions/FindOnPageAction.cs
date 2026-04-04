public class FindOnPageAction : KeyComboMacroAction
{
    public override string ActionId => "find_on_page";
    public override string DisplayName => "Find";
    protected override byte[] Modifiers => new byte[] { VK_CONTROL };
    protected override byte MainKey => 0x46; // F
}
