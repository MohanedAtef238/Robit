using System.Collections.Generic;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string[] GetFiles(string path, string searchPattern, bool recursive);
    byte[] ReadAllBytes(string path);
    string GetSpecialFolderPath(System.Environment.SpecialFolder folder);
}
