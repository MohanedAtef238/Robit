using LnkParser.Constants;

public class ZoomInAction : KeyComboMacroAction
{
    public override string ActionId => "zoom_in";
    public override string DisplayName => "Zoom In";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.OEMPlus;
}
