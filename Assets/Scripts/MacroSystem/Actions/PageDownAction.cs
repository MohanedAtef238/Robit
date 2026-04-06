using System;
using LnkParser.Constants;

public class PageDownAction : KeyComboMacroAction
{
    public override string ActionId => "page_down";
    public override string DisplayName => "Page Down";
    protected override VirtualKeys[] Modifiers => Array.Empty<VirtualKeys>();
    protected override VirtualKeys MainKey => VirtualKeys.Next;
}
