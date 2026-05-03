using LnkParser.Constants;
using UnityEngine.Scripting;

public class UndoAction : KeyComboMacroAction
{
    [Preserve]
    static UndoAction() => MacroActionFactory.Register(MacroActionType.Undo, () => new UndoAction());

    public override string ActionId => "undo";
    public override string DisplayName => "Undo";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.Z;
}

