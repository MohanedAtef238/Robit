using LnkParser.Constants;
using UnityEngine.Scripting;

public class FindOnPageAction : KeyComboMacroAction
{
    [Preserve]
    static FindOnPageAction() => MacroActionFactory.Register(MacroActionType.FindOnPage, () => new FindOnPageAction());

    public override string ActionId => "find_on_page";
    public override string DisplayName => "Find";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.F;
}

