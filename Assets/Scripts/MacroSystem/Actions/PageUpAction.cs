using System;
using LnkParser.Constants;
using UnityEngine.Scripting;

public class PageUpAction : KeyComboMacroAction
{
    [Preserve]
    static PageUpAction() => MacroActionFactory.Register(MacroActionType.PageUp, () => new PageUpAction());

    public override string ActionId => "page_up";
    public override string DisplayName => "Page Up";
    protected override VirtualKeys[] Modifiers => Array.Empty<VirtualKeys>();
    protected override VirtualKeys MainKey => VirtualKeys.Prior;
}

