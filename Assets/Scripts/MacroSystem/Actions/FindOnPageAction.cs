using LnkParser.Constants;

public class FindOnPageAction : KeyComboMacroAction
{
    public override string ActionId => "find_on_page";
    public override string DisplayName => "Find";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Control };
    protected override VirtualKeys MainKey => VirtualKeys.F;
}
