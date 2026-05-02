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
    public static DesktopParser Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
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

                if (!IsValidShortcut(shortcut.TargetPath))
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

private Texture2D ExtractHighQualityIcon(string filePath)
{
    string iconsFolder = Path.Combine(Application.dataPath, "Icons");
    string exeName = Path.GetFileNameWithoutExtension(filePath);
    string pngCachePath = Path.Combine(iconsFolder, $"{exeName}_icon.png");

    // Check for cached PNG first (much faster than ICO processing)
    if (File.Exists(pngCachePath))
    {
        Debug.Log($"[DesktopParser] Using cached PNG icon: {pngCachePath}");
        byte[] pngData = File.ReadAllBytes(pngCachePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (tex.LoadImage(pngData))
        {
            return tex;
        }
        else
        {
            // tex was created but LoadImage failed — must Destroy to free the GPU resource
            UnityEngine.Object.Destroy(tex);
            Debug.LogWarning($"[DesktopParser] Failed to load cached PNG, falling back to extraction");
        }
    }

    // Extract ICO data directly to memory (no disk I/O for ICO files)
    List<byte[]> icoDatas = IconExtractor.ExtractAllIcos(filePath);
    if (icoDatas == null || icoDatas.Count == 0) return null;

    // Use the first ICO data (highest-res typically comes first)
    byte[] icoData = icoDatas[0];

    try
    {
        // Load the ICO from memory with Doji.Ico
        var iconFile = IcoConversion.LoadIcon(icoData);
        int largestIndex = 0;
        int maxSize = 0;

        for (int i = 0; i < iconFile.NumImages; i++)
        {
            int size = iconFile.Images[i].Width * iconFile.Images[i].Height;
            if (size > maxSize)
            {
                maxSize = size;
                largestIndex = i;
            }
        }

        Texture2D tex = iconFile.ExtractTexture2D(largestIndex);

        // Cache the highest resolution texture as PNG for future use
        try
        {
            Directory.CreateDirectory(iconsFolder);
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(pngCachePath, pngData);
            Debug.Log($"[DesktopParser] Cached PNG icon: {pngCachePath}");
        }
        catch (Exception cacheEx)
        {
            Debug.LogWarning($"[DesktopParser] Failed to cache PNG: {cacheEx.Message}");
        }

        return tex;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[DesktopParser] Failed to process ICO data from {filePath} | {e.Message}");
        return null;
    }
}

    public bool IsValidShortcut(string targetPath)
    {
        if (string.IsNullOrEmpty(targetPath))
            return false;

        return targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    public void AddShortcut(string name, string targetPath, Texture2D icon)
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