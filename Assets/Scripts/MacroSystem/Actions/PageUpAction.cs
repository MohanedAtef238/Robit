using System;
using LnkParser.Constants;

public class PageUpAction : KeyComboMacroAction
{
    public override string ActionId => "page_up";
    public override string DisplayName => "Page Up";
    protected override VirtualKeys[] Modifiers => Array.Empty<VirtualKeys>();
    protected override VirtualKeys MainKey => VirtualKeys.Prior;
}

