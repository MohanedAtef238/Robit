using System;

public class PageDownAction : KeyComboMacroAction
{
    public override string ActionId => "page_down";
    public override string DisplayName => "Page Down";
    protected override byte[] Modifiers => Array.Empty<byte>();
    protected override byte MainKey => 0x22; // Page Down
}
