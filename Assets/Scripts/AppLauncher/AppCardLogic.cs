using System.Collections.Generic;

public class AppCardModel
{
    public string Name { get; set; }
    public string TargetPath { get; set; }
    public string WorkingDirectory { get; set; }
    public bool HasIcon { get; set; }
}

public interface IAppCardLogic
{
    List<AppCardModel> GenerateCardModels(List<ShortcutInfo> shortcuts);
}

public class AppCardLogic : IAppCardLogic
{
    public List<AppCardModel> GenerateCardModels(List<ShortcutInfo> shortcuts)
    {
        var models = new List<AppCardModel>();
        if (shortcuts == null) return models;

        foreach (var shortcut in shortcuts)
        {
            models.Add(new AppCardModel
            {
                Name = shortcut.Name,
                TargetPath = shortcut.TargetPath,
                WorkingDirectory = shortcut.WorkingDirectory,
                HasIcon = shortcut.Icon != null
            });
        }

        return models;
    }
}
