using LnkParser.Constants;
using UnityEngine.Scripting;

public class ZoomInAction : KeyComboMacroAction
{
    [Preserve]
    static ZoomInAction() => MacroActionFactory.Register(MacroActionType.ZoomIn, () => new ZoomInAction());

    public override string ActionId => "zoom_in";
    public override string DisplayName => "Zoom In";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.OEMPlus;
}

