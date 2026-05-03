using LnkParser.Constants;
using UnityEngine.Scripting;

public class NewTabAction : KeyComboMacroAction
{
    [Preserve]
    static NewTabAction() => MacroActionFactory.Register(MacroActionType.NewTab, () => new NewTabAction());

    public override string ActionId => "new_tab";
    public override string DisplayName => "New Tab";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.T;
}

