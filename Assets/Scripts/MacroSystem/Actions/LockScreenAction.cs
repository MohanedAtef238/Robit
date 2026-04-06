using LnkParser.Constants;

public class LockScreenAction : KeyComboMacroAction
{
    public override string ActionId => "lock_screen";
    public override string DisplayName => "Lock";
    protected override VirtualKeys[] Modifiers => new[] { VirtualKeys.LeftWindows };
    protected override VirtualKeys MainKey => VirtualKeys.L;
    protected override bool FocusBehind => false;
}
