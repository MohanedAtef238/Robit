using LnkParser.Constants;

public class ScreenshotAction : KeyComboMacroAction
{
    public override string ActionId => "screenshot";
    public override string DisplayName => "Screenshot";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Snapshot;
}
