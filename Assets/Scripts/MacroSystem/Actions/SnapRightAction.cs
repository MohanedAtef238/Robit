using LnkParser.Constants;
using UnityEngine.Scripting;

public class SnapRightAction : KeyComboMacroAction
{
    [Preserve]
    static SnapRightAction() => MacroActionFactory.Register(MacroActionType.SnapRight, () => new SnapRightAction());

    public override string ActionId => "snap_right";
    public override string DisplayName => "Snap Right";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Right;
}

