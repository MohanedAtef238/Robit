using System.Collections.Generic;

public interface IAppLauncher
{
    List<ShortcutInfo> CachedShortcuts { get; set; }
    void LaunchApplication(string path, string workingDirectory = null);
    void ReturnToDesktop();
    void GoHome();
}
