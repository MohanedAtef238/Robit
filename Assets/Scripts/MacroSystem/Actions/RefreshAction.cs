using LnkParser.Constants;
using UnityEngine.Scripting;

public class RefreshAction : KeyComboMacroAction
{
    [Preserve]
    static RefreshAction() => MacroActionFactory.Register(MacroActionType.Refresh, () => new RefreshAction());

    public override string ActionId => "refresh";
    public override string DisplayName => "Refresh";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.R;
}

