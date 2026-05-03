using LnkParser.Constants;
using UnityEngine.Scripting;

public class MaximizeRestoreAction : KeyComboMacroAction
{
    [Preserve]
    static MaximizeRestoreAction() => MacroActionFactory.Register(MacroActionType.MaximizeRestore, () => new MaximizeRestoreAction());

    public override string ActionId => "maximize_restore";
    public override string DisplayName => "Maximize";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Up;
}

