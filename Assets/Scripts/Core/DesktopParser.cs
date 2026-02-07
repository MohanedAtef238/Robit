using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using LnkParser;

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
    public bool enableDiskCache = true;
    public bool enableMemoryCache = true;
    
    private static readonly Dictionary<string, string> IconUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"chrome", "https://img.icons8.com/color/96/chrome--v1.png"},
        {"google chrome", "https://img.icons8.com/color/96/chrome--v1.png"},
        {"googlechrome", "https://img.icons8.com/color/96/chrome--v1.png"},
        {"firefox", "https://img.icons8.com/color/96/firefox--v1.png"},
        {"mozilla firefox", "https://img.icons8.com/color/96/firefox--v1.png"},
        {"msedge", "https://img.icons8.com/color/96/ms-edge-new.png"},
        {"microsoft edge", "https://img.icons8.com/color/96/ms-edge-new.png"},
        {"edge", "https://img.icons8.com/color/96/ms-edge-new.png"},
        {"brave", "https://img.icons8.com/color/96/brave-web-browser.png"},
        {"brave browser", "https://img.icons8.com/color/96/brave-web-browser.png"},
        {"opera", "https://img.icons8.com/color/96/opera--v1.png"},
        {"opera gx", "https://img.icons8.com/color/96/opera-gx.png"},
        {"opera browser", "https://img.icons8.com/color/96/opera--v1.png"},
        {"vivaldi", "https://img.icons8.com/color/96/vivaldi-web-browser.png"},
        {"safari", "https://img.icons8.com/color/96/safari--v1.png"},
        {"chromium", "https://img.icons8.com/color/96/chrome--v1.png"},
        {"tor browser", "https://img.icons8.com/color/96/tor-browser.png"},
        {"tor", "https://img.icons8.com/color/96/tor-browser.png"},

        {"discord", "https://img.icons8.com/color/96/discord-logo.png"},
        {"whatsapp", "https://img.icons8.com/color/96/whatsapp--v1.png"},
        {"whatsapp desktop", "https://img.icons8.com/color/96/whatsapp--v1.png"},
        {"telegram", "https://img.icons8.com/color/96/telegram-app--v1.png"},
        {"telegram desktop", "https://img.icons8.com/color/96/telegram-app--v1.png"},
        {"signal", "https://img.icons8.com/color/96/signal-app.png"},
        {"signal desktop", "https://img.icons8.com/color/96/signal-app.png"},
        {"slack", "https://img.icons8.com/color/96/slack-new.png"},
        {"microsoft teams", "https://img.icons8.com/color/96/microsoft-teams-2019.png"},
        {"teams", "https://img.icons8.com/color/96/microsoft-teams-2019.png"},
        {"ms teams", "https://img.icons8.com/color/96/microsoft-teams-2019.png"},
        {"skype", "https://img.icons8.com/color/96/skype--v1.png"},
        {"skype for business", "https://img.icons8.com/color/96/skype--v1.png"},
        {"zoom", "https://img.icons8.com/color/96/zoom.png"},
        {"zoom workplace", "https://img.icons8.com/color/96/zoom.png"},
        {"messenger", "https://img.icons8.com/color/96/facebook-messenger--v1.png"},
        {"facebook messenger", "https://img.icons8.com/color/96/facebook-messenger--v1.png"},
        {"viber", "https://img.icons8.com/color/96/viber.png"},
        {"wechat", "https://img.icons8.com/color/96/weixing.png"},
        {"line", "https://img.icons8.com/color/96/line-me.png"},
        {"thunderbird", "https://img.icons8.com/color/96/mozilla-thunderbird.png"},
        {"mozilla thunderbird", "https://img.icons8.com/color/96/mozilla-thunderbird.png"},
        {"outlook", "https://img.icons8.com/color/96/microsoft-outlook-2019--v2.png"},
        {"microsoft outlook", "https://img.icons8.com/color/96/microsoft-outlook-2019--v2.png"},
    };

    private readonly Dictionary<string, Texture2D> memoryCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private string cacheDir;
    
    private static readonly HashSet<string> AllowedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "google chrome", "googlechrome",
        "firefox", "mozilla firefox",
        "msedge", "microsoft edge", "edge",
        "brave", "brave browser",
        "opera", "opera gx", "opera browser",
        "vivaldi",
        "safari",
        "chromium",
        "arc", "arc browser",
        "tor browser", "tor",
        "waterfox",
        "librewolf",
        "floorp",
        "zen browser",
        
        "discord",
        "whatsapp", "whatsapp desktop",
        "telegram", "telegram desktop",
        "signal", "signal desktop",
        "slack",
        "microsoft teams", "teams", "ms teams",
        "skype", "skype for business",
        "zoom", "zoom workplace",
        "messenger", "facebook messenger",
        "viber",
        "wechat",
        "line",
        "wire",
        "element", "element desktop",
        "guilded",
        "mattermost",
        "zulip",
        "thunderbird", "mozilla thunderbird",
        "outlook", "microsoft outlook",
        "keybase",
    };

    void Start()
    {
        cacheDir = Path.Combine(Application.persistentDataPath, "IconCache");
        if (enableDiskCache && !Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
        StartCoroutine(ParseShortcuts());
    }
    
    IEnumerator ParseShortcuts()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var publicDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        var shortcutFiles = new List<string>();
        shortcutFiles.AddRange(Directory.GetFiles(desktopPath, "*.lnk"));
        shortcutFiles.AddRange(Directory.GetFiles(publicDesktopPath, "*.lnk"));

        Debug.Log($"[DesktopParser] Found {shortcutFiles.Count} total shortcuts on desktop");

        var pendingShortcuts = new List<(string name, string targetPath, string iconUrl)>();
        
        foreach (var file in shortcutFiles)
        {
            try
            {
                var shortcutName = Path.GetFileNameWithoutExtension(file);
                var shortcut = new WinShortcut(file);
                
                if (string.IsNullOrEmpty(shortcut.TargetPath))
                {
                    Debug.LogWarning($"[DesktopParser] Skipping '{shortcutName}': No target path");
                    continue;
                }
                
                if (!File.Exists(shortcut.TargetPath))
                {
                    Debug.LogWarning($"[DesktopParser] Skipping '{shortcutName}': Target does not exist: {shortcut.TargetPath}");
                    continue;
                }
                
                var exeName = Path.GetFileNameWithoutExtension(shortcut.TargetPath);
                if (!IsAllowedApp(shortcutName, exeName))
                {
                    Debug.Log($"[DesktopParser] Skipping '{shortcutName}' (exe: {exeName}): Not in whitelist");
                    continue;
                }
                
                string iconUrl = GetIconUrl(shortcutName, exeName);
                if (string.IsNullOrEmpty(iconUrl))
                {
                    Debug.LogWarning($"[DesktopParser] No icon URL found for '{shortcutName}'");
                    continue;
                }
                
                Debug.Log($"[DesktopParser] Queued allowed app: '{shortcutName}' -> {shortcut.TargetPath}");
                pendingShortcuts.Add((shortcutName, shortcut.TargetPath, iconUrl));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DesktopParser] Failed to parse shortcut: {file}, Error: {e.Message}");
            }
        }
        
        Debug.Log($"[DesktopParser] Found {pendingShortcuts.Count} valid shortcuts, fetching icons...");
        
        foreach (var (name, targetPath, iconUrl) in pendingShortcuts)
        {
            yield return StartCoroutine(FetchIconAndAddShortcut(name, targetPath, iconUrl));
        }
        
        Debug.Log($"[DesktopParser] Finished parsing. Total shortcuts: {shortcuts.Count}");
        parsingComplete = true;
    }
    
    IEnumerator FetchIconAndAddShortcut(string name, string targetPath, string iconUrl)
    {
        Debug.Log($"[DesktopParser] Fetching icon from: {iconUrl}");

        string cacheKey = $"{name}|{iconUrl}";
        if (enableMemoryCache && memoryCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
        {
            Debug.Log($"[DesktopParser] Using memory cached icon for '{name}'");
            AddShortcut(name, targetPath, cachedTexture);
            yield break;
        }
        // Search in cache first
        string cachedPath = null;
        if (enableDiskCache)
        {
            string fileName = $"{GetSafeFileName(cacheKey)}.png";
            cachedPath = Path.Combine(cacheDir, fileName);
            if (File.Exists(cachedPath))
            {
                byte[] bytes = File.ReadAllBytes(cachedPath);
                Texture2D diskTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (diskTexture.LoadImage(bytes))
                {
                    Debug.Log($"[DesktopParser] Loaded disk cached icon for '{name}'");
                    if (enableMemoryCache)
                        memoryCache[cacheKey] = diskTexture;
                    AddShortcut(name, targetPath, diskTexture);
                    yield break;
                }
            }
        }

        //For miss, fetch then write to cache

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(iconUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Debug.Log($"[DesktopParser] Downloaded icon for '{name}': {texture.width}x{texture.height}");
            if (enableDiskCache && cachedPath != null)
            {
                byte[] pngBytes = texture.EncodeToPNG();
                if (pngBytes != null && pngBytes.Length > 0)
                {
                    File.WriteAllBytes(cachedPath, pngBytes);
                }
            }
            if (enableMemoryCache)
                memoryCache[cacheKey] = texture;
            AddShortcut(name, targetPath, texture);
        }
        else
        {
            Debug.LogError($"[DesktopParser] Failed to fetch icon for '{name}': {request.error}");
            AddShortcut(name, targetPath, null);
        }

        request.Dispose();
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
        Debug.Log($"[DesktopParser] SUCCESS: Added shortcut '{name}'");
    }
    
    private string GetIconUrl(string shortcutName, string exeName)
    {
        if (IconUrls.TryGetValue(shortcutName, out string url))
            return url;
        
        if (IconUrls.TryGetValue(exeName, out url))
            return url;
        
        foreach (var kvp in IconUrls)
        {
            if (shortcutName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                return kvp.Value;
            if (exeName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                return kvp.Value;
        }
        
        return null;
    }
    
    private bool IsAllowedApp(string shortcutName, string exeName)
    {
        if (AllowedApps.Contains(shortcutName))
            return true;
        
        if (AllowedApps.Contains(exeName))
            return true;
        
        foreach (var app in AllowedApps)
        {
            if (shortcutName.IndexOf(app, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (exeName.IndexOf(app, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        
        return false;
    }

    private string GetSafeFileName(string input)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input;
    }
}
