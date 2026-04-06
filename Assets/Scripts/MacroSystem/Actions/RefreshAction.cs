using LnkParser.Constants;

public class RefreshAction : KeyComboMacroAction
{
    public override string ActionId => "refresh";
    public override string DisplayName => "Refresh";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.R;
}
