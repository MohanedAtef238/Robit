using LnkParser.Constants;
using UnityEngine.Scripting;

public class CloseWindowAction : KeyComboMacroAction
{
    [Preserve]
    static CloseWindowAction() => MacroActionFactory.Register(MacroActionType.CloseWindow, () => new CloseWindowAction());

    public override string ActionId => "close_window";
    public override string DisplayName => "Close";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.F4;
}

