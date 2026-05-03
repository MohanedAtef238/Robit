using System.IO;
using System.Collections.Generic;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    
    public string[] GetFiles(string path, string searchPattern, bool recursive)
    {
        return Directory.GetFiles(path, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public string GetSpecialFolderPath(System.Environment.SpecialFolder folder)
    {
        return System.Environment.GetFolderPath(folder);
    }
}
