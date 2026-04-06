using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;

public static class IconExtractor
{
    const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
    static readonly IntPtr RT_ICON = (IntPtr)3;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll")]
    static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll")]
    static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

    delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct GRPICONDIR
    {
        public ushort idReserved;
        public ushort idType;
        public ushort idCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct GRPICONDIRENTRY
    {
        public byte bWidth;
        public byte bHeight;
        public byte bColorCount;
        public byte bReserved;
        public ushort wPlanes;
        public ushort wBitCount;
        public uint dwBytesInRes;
        public ushort nID;
    }

    /// <summary>
    /// Extracts all icon groups from an EXE/DLL as ICO byte arrays.
    /// </summary>
    public static List<byte[]> ExtractAllIcos(string exePath)
    {
        IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        if (hModule == IntPtr.Zero) return null;

        var icoList = new List<byte[]>();

        try
        {
            EnumResNameProc callback = (h, type, name, lParam) =>
            {
                try
                {
                    IntPtr hRes = FindResource(hModule, name, RT_GROUP_ICON);
                    if (hRes != IntPtr.Zero)
                    {
                        IntPtr hData = LoadResource(hModule, hRes);
                        IntPtr pData = LockResource(hData);
                        uint size = SizeofResource(hModule, hRes);
                        byte[] groupData = new byte[size];
                        Marshal.Copy(pData, groupData, 0, (int)size);

                        byte[] icoData = BuildIcoFromGroup(hModule, groupData);
                        if (icoData != null) icoList.Add(icoData);
                    }
                }
                catch { /* ignore invalid resource */ }

                return true; // continue enumerating
            };

            EnumResourceNames(hModule, RT_GROUP_ICON, callback, IntPtr.Zero);
        }
        finally
        {
            FreeLibrary(hModule);
        }

        return icoList.Count > 0 ? icoList : null;
    }

    /// <summary>
    /// Extracts all icons and saves them as .ico files in the Assets/Icons folder.
    /// Returns the list of saved file paths.
    /// </summary>
    public static List<string> ExtractAndSaveAllIcos(string exePath)
    {
        var icos = ExtractAllIcos(exePath);
        if (icos == null || icos.Count == 0) return null;

        string iconsFolder = Path.Combine(Application.dataPath, "Icons");
        Directory.CreateDirectory(iconsFolder);

        var savedPaths = new List<string>();
        string exeName = Path.GetFileNameWithoutExtension(exePath);

        for (int i = 0; i < icos.Count; i++)
        {
            string icoPath = Path.Combine(iconsFolder, $"{exeName}_{i}.ico");
            try
            {
                File.WriteAllBytes(icoPath, icos[i]);
                Debug.Log($"Saved ICO: {icoPath}");
                savedPaths.Add(icoPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save ICO file: {ex.Message}");
            }
        }

        return savedPaths;
    }

    static byte[] BuildIcoFromGroup(IntPtr hModule, byte[] groupData)
    {
        int offset = 0;
        GRPICONDIR dir = ByteArrayToStructure<GRPICONDIR>(groupData, ref offset);

        GRPICONDIRENTRY[] entries = new GRPICONDIRENTRY[dir.idCount];
        for (int i = 0; i < dir.idCount; i++)
        {
            entries[i] = ByteArrayToStructure<GRPICONDIRENTRY>(groupData, ref offset);
        }

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((ushort)0); // reserved
            writer.Write((ushort)1); // type = icon
            writer.Write((ushort)entries.Length);

            int imageOffset = 6 + (16 * entries.Length);
            var imageDatas = new byte[entries.Length][];

            for (int i = 0; i < entries.Length; i++)
            {
                imageDatas[i] = GetIconData(hModule, entries[i].nID);

                writer.Write(entries[i].bWidth);
                writer.Write(entries[i].bHeight);
                writer.Write(entries[i].bColorCount);
                writer.Write(entries[i].bReserved);
                writer.Write(entries[i].wPlanes);
                writer.Write(entries[i].wBitCount);
                writer.Write((uint)imageDatas[i].Length);
                writer.Write((uint)imageOffset);

                imageOffset += imageDatas[i].Length;
            }

            foreach (var img in imageDatas)
                writer.Write(img);

            return ms.ToArray();
        }
    }

    static byte[] GetIconData(IntPtr hModule, ushort resourceId)
    {
        IntPtr hRes = FindResource(hModule, (IntPtr)resourceId, RT_ICON);
        IntPtr hData = LoadResource(hModule, hRes);
        IntPtr pData = LockResource(hData);
        uint size = SizeofResource(hModule, hRes);

        byte[] data = new byte[size];
        Marshal.Copy(pData, data, 0, (int)size);
        return data;
    }

    static T ByteArrayToStructure<T>(byte[] bytes, ref int offset)
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(bytes, offset, ptr, size);
        T obj = (T)Marshal.PtrToStructure(ptr, typeof(T));
        Marshal.FreeHGlobal(ptr);

        offset += size;
        return obj;
    }
}