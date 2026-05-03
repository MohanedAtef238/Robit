using LnkParser.Constants;

public class ZoomOutAction : KeyComboMacroAction
{
    public override string ActionId => "zoom_out";
    public override string DisplayName => "Zoom Out";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.OEMMinus;
}

