using System;

public class PageUpAction : KeyComboMacroAction
{
    public override string ActionId => "page_up";
    public override string DisplayName => "Page Up";
    protected override byte[] Modifiers => Array.Empty<byte>();
    protected override byte MainKey => 0x21; // Page Up
}
