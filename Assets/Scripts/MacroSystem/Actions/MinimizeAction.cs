using LnkParser.Constants;
using UnityEngine.Scripting;

public class MinimizeAction : KeyComboMacroAction
{
    [Preserve]
    static MinimizeAction() => MacroActionFactory.Register(MacroActionType.Minimize, () => new MinimizeAction());

    public override string ActionId => "minimize";
    public override string DisplayName => "Minimize";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Down;
}

