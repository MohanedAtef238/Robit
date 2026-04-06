using UnityEngine;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using LnkParser;
using System.Linq;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Doji.Ico;
using UnityEngine.UI;

public struct ShortcutInfo
{
    public string Name;
    public string TargetPath;
    public string WorkingDirectory;
    public Texture2D Icon;
}

public class DesktopParser : MonoBehaviour
{
    public List<ShortcutInfo> shortcuts = new List<ShortcutInfo>();
    public bool parsingComplete = false;

    void Start()
    {
        StartCoroutine(ParseShortcuts());
    }

    IEnumerator ParseShortcuts()
    {
        string[] startMenuPaths = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        var shortcutFiles = startMenuPaths
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.lnk", SearchOption.AllDirectories))
            .ToList();

        Debug.Log($"[DesktopParser] Found {shortcutFiles.Count} shortcuts");

        foreach (var file in shortcutFiles)
        {
            try
            {
                var shortcut = new WinShortcut(file);

                if (string.IsNullOrEmpty(shortcut.TargetPath))
                    continue;

                // 🔥 Optional filter (recommended)
                if (!shortcut.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!File.Exists(shortcut.TargetPath))
                    continue;

                string name = Path.GetFileNameWithoutExtension(file);

                Texture2D icon = ExtractHighQualityIcon(shortcut.TargetPath);

                if (icon == null)
                {
                    Debug.LogWarning($"[DesktopParser] Icon failed: {name}");
                }

                AddShortcut(name, shortcut.TargetPath, icon);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DesktopParser] Failed: {file} | {e.Message}");
            }

            yield return null;
        }

        parsingComplete = true;
        Debug.Log($"[DesktopParser] Done. Total: {shortcuts.Count}");
    }

    // 🔥 BEST ICON EXTRACTION METHOD
private Texture2D ExtractHighQualityIcon(string filePath)
{
    // Extract and save ICOs to Icons folder
    List<string> icoPaths = IconExtractor.ExtractAndSaveAllIcos(filePath);
    if (icoPaths == null || icoPaths.Count == 0) return null;

    // Use the first ICO file (highest-res typically comes first)
    string icoPath = icoPaths[0];

    try
    {
        // Load the saved ICO file with Doji.Ico
        var iconFile = IcoConversion.LoadIcon(icoPath);
        Texture2D tex = iconFile.ExtractTexture2D(iconFile.NumImages-1); 
        return tex;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[DesktopParser] Failed to load ICO: {icoPath} | {e.Message}");
        return null;
    }
}

    private void AddShortcut(string name, string targetPath, Texture2D icon)
    {
        shortcuts.Add(new ShortcutInfo
        {
            Name = name,
            TargetPath = targetPath,
            WorkingDirectory = "",
            Icon = icon
        });

        Debug.Log($"[DesktopParser] Added: {name}");
    }
}