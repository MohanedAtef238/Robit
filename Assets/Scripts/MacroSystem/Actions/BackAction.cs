using LnkParser.Constants;
using UnityEngine.Scripting;

public class BackAction : KeyComboMacroAction
{
    [Preserve]
    static BackAction() => MacroActionFactory.Register(MacroActionType.Back, () => new BackAction());

    public override string ActionId => "back";
    public override string DisplayName => "Back";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Left;
}

