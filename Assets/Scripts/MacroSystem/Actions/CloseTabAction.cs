using LnkParser.Constants;
using UnityEngine.Scripting;

public class CloseTabAction : KeyComboMacroAction
{
    [Preserve]
    static CloseTabAction() => MacroActionFactory.Register(MacroActionType.CloseTab, () => new CloseTabAction());

    public override string ActionId => "close_tab";
    public override string DisplayName => "Close Tab";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.W;
}

