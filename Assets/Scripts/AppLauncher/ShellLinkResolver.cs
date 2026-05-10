using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Resolves Windows shortcut (.lnk) target paths using the Windows Shell IShellLink COM API.
/// This handles all shortcut types, including those using HasLinkTargetIDList (modern apps,
/// shortcuts created by installers, etc.) that LnkParser/WinShortcut cannot parse.
/// </summary>
public static class ShellLinkResolver
{
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject { }

    // SLR_NO_UI: don't show a resolve dialog if the target can't be found
    private const uint SLR_NO_UI = 0x1;
    // STGM_READ: open the file read-only
    private const uint STGM_READ = 0x0;

    /// <summary>
    /// Resolves the target executable path from a .lnk file using the Windows Shell API.
    /// Returns null if resolution fails or the COM API is unavailable.
    /// </summary>
    public static string Resolve(string lnkPath, out string workingDirectory)
    {
        workingDirectory = null;
        try
        {
            var linkObj = new ShellLinkObject();
            var shellLink = (IShellLinkW)linkObj;
            var persistFile = (IPersistFile)linkObj;

            persistFile.Load(lnkPath, STGM_READ);
            shellLink.Resolve(IntPtr.Zero, SLR_NO_UI);

            var pathBuf = new StringBuilder(260);
            shellLink.GetPath(pathBuf, pathBuf.Capacity, IntPtr.Zero, 0);

            var workDirBuf = new StringBuilder(260);
            shellLink.GetWorkingDirectory(workDirBuf, workDirBuf.Capacity);
            workingDirectory = workDirBuf.Length > 0 ? workDirBuf.ToString() : null;

            string path = pathBuf.ToString();
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }
}
