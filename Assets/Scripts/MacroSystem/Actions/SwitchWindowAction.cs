using LnkParser.Constants;
using UnityEngine.Scripting;

public class SwitchWindowAction : KeyComboMacroAction
{
    [Preserve]
    static SwitchWindowAction() => MacroActionFactory.Register(MacroActionType.SwitchWindow, () => new SwitchWindowAction());

    public override string ActionId => "switch_window";
    public override string DisplayName => "Switch Window";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Tab;
}

