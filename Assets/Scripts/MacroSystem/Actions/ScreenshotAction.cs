using LnkParser.Constants;
using UnityEngine.Scripting;

public class ScreenshotAction : KeyComboMacroAction
{
    [Preserve]
    static ScreenshotAction() => MacroActionFactory.Register(MacroActionType.Screenshot, () => new ScreenshotAction());

    public override string ActionId => "screenshot";
    public override string DisplayName => "Screenshot";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Snapshot;
}

