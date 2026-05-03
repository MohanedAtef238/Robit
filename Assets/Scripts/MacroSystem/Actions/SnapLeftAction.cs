using LnkParser.Constants;
using UnityEngine.Scripting;

public class SnapLeftAction : KeyComboMacroAction
{
    [Preserve]
    static SnapLeftAction() => MacroActionFactory.Register(MacroActionType.SnapLeft, () => new SnapLeftAction());

    public override string ActionId => "snap_left";
    public override string DisplayName => "Snap Left";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.Left;
}

