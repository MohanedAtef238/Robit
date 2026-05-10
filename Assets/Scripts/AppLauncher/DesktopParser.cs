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

    private IFileSystem _fileSystem = new PhysicalFileSystem();
    public IFileSystem FileSystem { get => _fileSystem; set => _fileSystem = value; }

    IEnumerator ParseShortcuts()
    {
        string[] desktopPaths = new string[]
        {
            _fileSystem.GetSpecialFolderPath(Environment.SpecialFolder.Desktop),
            _fileSystem.GetSpecialFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        var shortcutFiles = new List<string>();
        foreach (var path in desktopPaths)
        {
            if (_fileSystem.DirectoryExists(path))
            {
                shortcutFiles.AddRange(_fileSystem.GetFiles(path, "*.lnk", true));
            }
        }

        RobitLogger.Log($"[DesktopParser] Found {shortcutFiles.Count} shortcuts");

        foreach (var file in shortcutFiles)
        {
            try
            {
                string targetPath = null;
                string workingDirectory = null;

                try
                {
                    byte[] shortcutData = _fileSystem.ReadAllBytes(file);
                    var shortcut = new WinShortcut(new MemoryStream(shortcutData));
                    targetPath = shortcut.TargetPath;
                    workingDirectory = shortcut.WorkingDirectory;
                }
                catch
                {
                    // WinShortcut can't parse ItemIDList-based shortcuts (modern installers,
                    // GPU-Z, Unity Hub, etc.). Fall back to the Windows Shell IShellLink COM API
                    // which resolves all .lnk types including those without LocalBasePath.
                    targetPath = ShellLinkResolver.Resolve(file, out workingDirectory);
                }

                if (!IsValidShortcut(targetPath))
                    continue;

                if (!_fileSystem.FileExists(targetPath))
                    continue;

                string name = Path.GetFileNameWithoutExtension(file);

                Texture2D icon = ExtractHighQualityIcon(targetPath);

                if (icon == null)
                {
                    RobitLogger.LogWarning($"[DesktopParser] Icon failed: {name}");
                }

                AddShortcut(name, targetPath, icon);
            }
            catch (Exception e)
            {
                RobitLogger.LogWarning($"[DesktopParser] Failed: {file} | {e.Message}");
            }

            yield return null;
        }

        parsingComplete = true;
        RobitLogger.Log($"[DesktopParser] Done. Total: {shortcuts.Count}");
    }

private Texture2D ExtractHighQualityIcon(string filePath)
{
    string iconsFolder = Path.Combine(Application.dataPath, "Icons");
    string exeName = Path.GetFileNameWithoutExtension(filePath);
    string pngCachePath = Path.Combine(iconsFolder, $"{exeName}_icon.png");

    // Check for cached PNG first
    if (_fileSystem.FileExists(pngCachePath))
    {
        RobitLogger.Log($"[DesktopParser] Using cached PNG icon: {pngCachePath}");
        byte[] pngData = _fileSystem.ReadAllBytes(pngCachePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (tex.LoadImage(pngData))
        {
            return tex;
        }
        else
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(tex);
            else
                UnityEngine.Object.DestroyImmediate(tex);

            RobitLogger.LogWarning($"[DesktopParser] Failed to load cached PNG, falling back to extraction");
        }
    }

    // Extract ICO data directly to memory
    List<byte[]> icoDatas = IconExtractor.ExtractAllIcos(filePath);
    if (icoDatas == null || icoDatas.Count == 0) return null;

    byte[] icoData = icoDatas[0];

    try
    {
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

        // Cache the highest resolution texture as PNG
        try
        {
            // Note: We don't use fileSystem for directory creation/writing in the real version yet
            // to avoid complicating the mock for now, but we'll use it for FileExists above.
            if (!Directory.Exists(iconsFolder))
                Directory.CreateDirectory(iconsFolder);
            
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(pngCachePath, pngData);
            RobitLogger.Log($"[DesktopParser] Cached PNG icon: {pngCachePath}");
        }
        catch (Exception cacheEx)
        {
            RobitLogger.LogWarning($"[DesktopParser] Failed to cache PNG: {cacheEx.Message}");
        }

        return tex;
    }
    catch (Exception e)
    {
        RobitLogger.LogWarning($"[DesktopParser] Failed to process ICO data from {filePath} | {e.Message}");
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

        RobitLogger.Log($"[DesktopParser] Added: {name}");
    }
}
