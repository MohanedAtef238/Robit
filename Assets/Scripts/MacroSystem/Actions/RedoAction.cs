using LnkParser.Constants;
using UnityEngine.Scripting;

public class RedoAction : KeyComboMacroAction
{
    [Preserve]
    static RedoAction() => MacroActionFactory.Register(MacroActionType.Redo, () => new RedoAction());

    public override string ActionId => "redo";
    public override string DisplayName => "Redo";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.Y;
}

