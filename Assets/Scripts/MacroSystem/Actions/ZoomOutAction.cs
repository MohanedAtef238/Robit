using LnkParser.Constants;
using UnityEngine.Scripting;

public class ZoomOutAction : KeyComboMacroAction
{
    [Preserve]
    static ZoomOutAction() => MacroActionFactory.Register(MacroActionType.ZoomOut, () => new ZoomOutAction());

    public override string ActionId => "zoom_out";
    public override string DisplayName => "Zoom Out";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.OEMMinus;
}

