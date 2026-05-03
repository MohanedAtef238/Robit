using LnkParser.Constants;
using UnityEngine.Scripting;

public class ForwardAction : KeyComboMacroAction
{
    [Preserve]
    static ForwardAction() => MacroActionFactory.Register(MacroActionType.Forward, () => new ForwardAction());

    public override string ActionId => "forward";
    public override string DisplayName => "Forward";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.Menu };
    protected override VirtualKeys MainKey => VirtualKeys.Right;
}

