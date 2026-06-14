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
                this.Parse(istream);
            }
        }

        // Initialize an instance of this class using an already-open stream.
        // Provided for unit testing — allows MemoryStream fixtures without disk access.
        public WinShortcut(Stream stream)
        {
            this.Parse(stream);
        }

        public static bool TryParse(Stream stream, out string targetPath, out string workingDirectory)
        {
            targetPath = null;
            workingDirectory = null;
            try
            {
                var shortcut = new WinShortcut(stream);
                targetPath = shortcut.TargetPath;
                workingDirectory = shortcut.WorkingDirectory;
                return !string.IsNullOrEmpty(targetPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The real path of target this shortcut refers to.
        public string TargetPath { get; private set; }
        public string IconLocation { get; private set; }
        public string WorkingDirectory { get; private set; }

        public bool IsDirectory { get; private set; }

        public string HotKey
        {
            get { return this._hotKey ?? ""; }
            private set { _hotKey = value; }
        }

        private void Parse(Stream istream)
        {
            try
            {
                var linkFlags = this.ParseHeader(istream);
                if ((linkFlags & Constants.LinkFlags.HasLinkTargetIdList) == Constants.LinkFlags.HasLinkTargetIdList)
                {
                    this.ParseTargetIDList(istream);
                }
                else
                {
                    // Ensure we skip the full 76-byte ShellLinkHeader even if IDList is missing
                    if (istream.CanSeek)
                    {
                        istream.Seek(76, SeekOrigin.Begin);
                    }
                }

                if ((linkFlags & Constants.LinkFlags.HasLinkInfo) == Constants.LinkFlags.HasLinkInfo)
                {
                    this.ParseLinkInfo(istream);
                }

                this.ParseStringData(istream, linkFlags);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse shortcut", ex);
            }
        }

        // Reads flags and file attributes from header.
        private int ParseHeader(Stream stream)
        {
            if (stream.Length < 76)
                throw new EndOfStreamException("Stream is too short to be a valid LNK file");

            stream.Seek(20, SeekOrigin.Begin);//jump to the LinkFlags part of ShellLinkHeader
            var buffer = new byte[4];
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new EndOfStreamException("Failed to read LinkFlags");
            var linkFlags = BitConverter.ToInt32(buffer, 0);

            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new EndOfStreamException("Failed to read FileAttributes");
            var fileAttrFlags = BitConverter.ToInt32(buffer, 0);
            IsDirectory = (fileAttrFlags & Constants.FileAttributes.Directory) == Constants.FileAttributes.Directory;

            stream.Seek(36, SeekOrigin.Current);//jump to the HotKey part
            if (stream.Read(buffer, 0, 2) != 2)
                throw new EndOfStreamException("Failed to read HotKey");

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
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new EndOfStreamException("Failed to read TargetIDList size");
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
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new EndOfStreamException("Failed to read LinkInfoFlags");
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

        private void ParseStringData(Stream stream, int linkFlags)
        {
            bool isUnicode = (linkFlags & Constants.LinkFlags.IsUnicode) == Constants.LinkFlags.IsUnicode;

            if ((linkFlags & Constants.LinkFlags.HasName) == Constants.LinkFlags.HasName)
                ReadStringData(stream, isUnicode);

            if ((linkFlags & Constants.LinkFlags.HasRelativePath) == Constants.LinkFlags.HasRelativePath)
                ReadStringData(stream, isUnicode);

            if ((linkFlags & Constants.LinkFlags.HasWorkingDir) == Constants.LinkFlags.HasWorkingDir)
                WorkingDirectory = ReadStringData(stream, isUnicode);

            if ((linkFlags & Constants.LinkFlags.HasArguments) == Constants.LinkFlags.HasArguments)
                ReadStringData(stream, isUnicode);

            if ((linkFlags & Constants.LinkFlags.HasIconLocation) == Constants.LinkFlags.HasIconLocation)
                IconLocation = ReadStringData(stream, isUnicode);
        }

        private string ReadStringData(Stream stream, bool isUnicode)
        {
            var sizeBuffer = new byte[2];
            if (stream.Read(sizeBuffer, 0, sizeBuffer.Length) != sizeBuffer.Length)
                throw new EndOfStreamException("Failed to read string data size");

            ushort charCount = BitConverter.ToUInt16(sizeBuffer, 0);
            if (charCount == 0)
                return string.Empty;

            int byteCount = isUnicode ? charCount * 2 : charCount;
            var dataBuffer = new byte[byteCount];

            if (stream.Read(dataBuffer, 0, dataBuffer.Length) != dataBuffer.Length)
                throw new EndOfStreamException("Failed to read string data content");

            return isUnicode
                ? Encoding.Unicode.GetString(dataBuffer)
                : Encoding.Default.GetString(dataBuffer);
        }
    }
}

