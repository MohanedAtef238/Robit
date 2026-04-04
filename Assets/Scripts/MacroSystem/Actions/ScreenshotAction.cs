public class ScreenshotAction : KeyComboMacroAction
{
    public override string ActionId => "screenshot";
    public override string DisplayName => "Screenshot";
    protected override byte[] Modifiers => new byte[] { VK_MENU };
    protected override byte MainKey => 0x2C; // Print Screen
}
