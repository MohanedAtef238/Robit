using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class RunnerPathResolver
{
    public static Dictionary<string, string> ParseEnvFile(string envPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = File.ReadAllLines(envPath);
        foreach (string raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string line = raw.Trim();
            if (line.StartsWith("#"))
                continue;

            int split = line.IndexOf('=');
            if (split <= 0)
                continue;

            string key = line.Substring(0, split).Trim();
            string value = line.Substring(split + 1).Trim();

            if (value.Length >= 2)
            {
                bool doubleQuoted = value.StartsWith("\"") && value.EndsWith("\"");
                bool singleQuoted = value.StartsWith("'") && value.EndsWith("'");
                if (doubleQuoted || singleQuoted)
                    value = value.Substring(1, value.Length - 2);
            }

            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }

        return result;
    }

    public static bool TryResolveEnvFilePath(string configuredEnvFileName, out string envPath, out string details)
    {
        envPath = null;
        string foundPath = null;
        var attempts = new List<string>();

        string fileName = string.IsNullOrWhiteSpace(configuredEnvFileName) ? "emg.env" : configuredEnvFileName.Trim().Trim('"');

        void TryPath(string candidate)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
            {
                return;
            }

            if (attempts.Contains(fullPath))
                return;

            attempts.Add(fullPath);
            if (foundPath == null && File.Exists(fullPath))
                foundPath = fullPath;
        }

        string dataPath = Application.dataPath;
        string buildRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
        string executableRoot = AppDomain.CurrentDomain.BaseDirectory;
        string streamingAssets = Application.streamingAssetsPath;

        TryPath(Path.Combine(buildRoot, fileName));
        TryPath(Path.Combine(executableRoot, fileName));
        TryPath(Path.Combine(streamingAssets, fileName));

        details = $"[EnvResolver] Configured='{fileName}'\n[EnvResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        envPath = foundPath;
        return foundPath != null;
    }

    public static bool TryResolvePath(string configuredPath, bool expectFile, out string resolvedPath, out string details)
    {
        resolvedPath = null;
        string foundPath = null;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            details = "[PathResolver] Configured path is empty.";
            return false;
        }

        string normalized = configuredPath.Trim().Trim('"');
        var attempts = new List<string>();

        bool Exists(string path) => expectFile ? File.Exists(path) : Directory.Exists(path);

        void TryCandidate(string candidate)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
            {
                return;
            }

            if (attempts.Contains(fullPath))
                return;

            attempts.Add(fullPath);
            if (foundPath == null && Exists(fullPath))
                foundPath = fullPath;
        }

        if (Path.IsPathRooted(normalized))
            TryCandidate(normalized);
        else
        {
            string dataPath = Application.dataPath;
            string buildRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
            string executableRoot = AppDomain.CurrentDomain.BaseDirectory;
            string streamingAssetsPath = Application.streamingAssetsPath;

            TryCandidate(Path.Combine(streamingAssetsPath, normalized));
            TryCandidate(Path.Combine(buildRoot, normalized));
            TryCandidate(Path.Combine(executableRoot, normalized));
            TryCandidate(Path.Combine(buildRoot, "..", normalized));
        }

        details = $"[PathResolver] Configured='{configuredPath}', Type={(expectFile ? "File" : "Directory")}\n" +
                  $"[PathResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        resolvedPath = foundPath;
        return foundPath != null;
    }
}
