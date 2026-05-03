using LnkParser.Constants;
using UnityEngine.Scripting;

public class LockScreenAction : KeyComboMacroAction
{
    [Preserve]
    static LockScreenAction() => MacroActionFactory.Register(MacroActionType.LockScreen, () => new LockScreenAction());

    public override string ActionId => "lock_screen";
    public override string DisplayName => "Lock";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.L;
    protected override bool FocusBehind => false;
}

