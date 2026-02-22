using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LnkParser
{
    // Represents partial properties of a Windows shortcut file.
    public class WinShortcut
    {
        private string _hotKey;

        // Initialize an instance of this class using the path of shortcut file.
        public WinShortcut(string path)
        {
            using (var istream = File.OpenRead(path))
            {
                try
                {
                    this.Parse(istream);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to parse this file as a Windowsshortcut", ex);
                }
            }
        }

        // The real path of target this shortcut refers to.
        public string TargetPath { get; private set; }

        public bool IsDirectory { get; private set; }

        public string HotKey
        {
            get { return this._hotKey ?? ""; }
            private set { _hotKey = value; }
        }

        private void Parse(Stream istream)
        {
            var linkFlags = this.ParseHeader(istream);
            if ((linkFlags & Constants.LinkFlags.HasLinkTargetIdList) == Constants.LinkFlags.HasLinkTargetIdList)
            {
                this.ParseTargetIDList(istream);
            }
            if ((linkFlags & Constants.LinkFlags.HasLinkInfo) == Constants.LinkFlags.HasLinkInfo)
            {
                this.ParseLinkInfo(istream);
            }
        }

        // Reads flags and file attributes from header.
        private int ParseHeader(Stream stream)
        {
            stream.Seek(20, SeekOrigin.Begin);//jump to the LinkFlags part of ShellLinkHeader
            var buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            var linkFlags = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, buffer.Length);//read next 4 bytes, that is FileAttributesileAttributes
            var fileAttrFlags = BitConverter.ToInt32(buffer, 0);
            IsDirectory = (fileAttrFlags & Constants.FileAttributes.Directory) == Constants.FileAttributes.Directory;

            stream.Seek(36, SeekOrigin.Current);//jump to the HotKey part
            stream.Read(buffer, 0, 2);

            var keys = new List<string>();
            var hotKeyLowByte = (Constants.VirtualKeys)buffer[0];
            var hotKeyHighByte = (Constants.VirtualKeys)buffer[1];
            if (hotKeyHighByte.HasFlag(Constants.VirtualKeys.HOTKEYF_CONTROL))
                keys.Add("ctrl");
            if (hotKeyHighByte.HasFlag(Constants.VirtualKeys.HOTKEYF_SHIFT))
                keys.Add("shift");
            if (hotKeyHighByte.HasFlag(Constants.VirtualKeys.HOTKEYF_ALT))
                keys.Add("alt");
            if (Enum.IsDefined(typeof(Constants.VirtualKeys), hotKeyLowByte))
                keys.Add(hotKeyLowByte.ToString());
            HotKey = String.Join("+", keys);

            return linkFlags;
        }

        // Skips the TargetIDList section.
        private void ParseTargetIDList(Stream stream)
        {
            stream.Seek(76, SeekOrigin.Begin);//jump to the LinkTargetIDList part
            var buffer = new byte[2];
            stream.Read(buffer, 0, buffer.Length);
            var size = BitConverter.ToInt16(buffer, 0);
            //the TargetIDList part isn't used currently, so just move the cursor forward
            stream.Seek(size, SeekOrigin.Current);
        }

        // Extracts the target path from LinkInfo.
        private void ParseLinkInfo(Stream stream)
        {
            var start = stream.Position;//save the start position of LinkInfo
            stream.Seek(8, SeekOrigin.Current);//jump to the LinkInfoFlags part
            var buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            var lnkInfoFlags = BitConverter.ToInt32(buffer, 0);
            if ((lnkInfoFlags & Constants.LinkInfoFlags.VolumeIDAndLocalBasePath) == Constants.LinkInfoFlags.VolumeIDAndLocalBasePath)
            {
                stream.Seek(4, SeekOrigin.Current);
                stream.Read(buffer, 0, buffer.Length);
                var localBasePathOffset = BitConverter.ToInt32(buffer, 0);
                var basePathOffset = start + localBasePathOffset;
                stream.Seek(basePathOffset, SeekOrigin.Begin);

                using (var ms = new MemoryStream())
                {
                    var b = 0;
                    //get raw bytes of LocalBasePath
                    while ((b = stream.ReadByte()) > 0)
                        ms.WriteByte((byte)b);

                    TargetPath = Encoding.Default.GetString(ms.ToArray());
                }
            }
        }
    }
}
