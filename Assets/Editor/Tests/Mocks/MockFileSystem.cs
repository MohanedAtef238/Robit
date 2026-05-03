using System.Collections.Generic;
using System;
using System.Linq;

public class MockFileSystem : IFileSystem
{
    public Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
    public HashSet<string> Directories = new HashSet<string>();
    public Dictionary<Environment.SpecialFolder, string> SpecialFolders = new Dictionary<Environment.SpecialFolder, string>();

    public bool DirectoryExists(string path) => Directories.Contains(path);
    public bool FileExists(string path) => Files.ContainsKey(path);

    public string[] GetFiles(string path, string searchPattern, bool recursive)
    {
        // Simple mock: just return all files that start with the path
        return Files.Keys
            .Where(f => f.StartsWith(path) && f.EndsWith(searchPattern.Replace("*", "")))
            .ToArray();
    }

    public byte[] ReadAllBytes(string path)
    {
        if (Files.TryGetValue(path, out var data)) return data;
        throw new System.IO.FileNotFoundException(path);
    }

    public string GetSpecialFolderPath(Environment.SpecialFolder folder)
    {
        if (SpecialFolders.TryGetValue(folder, out var path)) return path;
        return $"/mock/{folder}";
    }
}
