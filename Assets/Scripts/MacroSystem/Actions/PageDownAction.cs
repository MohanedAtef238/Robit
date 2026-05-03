using System;
using LnkParser.Constants;
using UnityEngine.Scripting;

public class PageDownAction : KeyComboMacroAction
{
    [Preserve]
    static PageDownAction() => MacroActionFactory.Register(MacroActionType.PageDown, () => new PageDownAction());

    public override string ActionId => "page_down";
    public override string DisplayName => "Page Down";
    protected override VirtualKeys[] Modifiers => Array.Empty<VirtualKeys>();
    protected override VirtualKeys MainKey => VirtualKeys.Next;
}

