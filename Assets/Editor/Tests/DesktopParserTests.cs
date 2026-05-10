using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;

namespace Robit.Tests
{
    public class DesktopParserTests
    {
        private GameObject _holder;
        private DesktopParser _parser;
        private MockFileSystem _mockFS;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("TestHolder");
            _parser = _holder.AddComponent<DesktopParser>();
            _mockFS = new MockFileSystem();
            _parser.FileSystem = _mockFS;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void IsValidShortcut_OnlyAcceptsExe()
        {
            // Act & Assert
            Assert.IsTrue(_parser.IsValidShortcut("test.exe"), "Should accept lowercase .exe");
            Assert.IsTrue(_parser.IsValidShortcut("TEST.EXE"), "Should accept uppercase .EXE");
            Assert.IsFalse(_parser.IsValidShortcut("test.lnk"), "Should reject .lnk (we only want the exe target)");
            Assert.IsFalse(_parser.IsValidShortcut("test.txt"), "Should reject .txt");
            Assert.IsFalse(_parser.IsValidShortcut("directory/path"), "Should reject path without extension");
            Assert.IsFalse(_parser.IsValidShortcut("C:/Windows/System32/"), "Should reject directory paths");
            Assert.IsFalse(_parser.IsValidShortcut(""), "Should reject empty string");
            Assert.IsFalse(_parser.IsValidShortcut(null), "Should reject null");
        }

        [UnityTest]
        public IEnumerator ParseShortcuts_HandlesEmptyDirectories()
        {
            // Arrange
            _mockFS.Directories.Add("/mock/Desktop");
            _mockFS.Directories.Add("/mock/CommonDesktop");
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.Desktop] = "/mock/Desktop";
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.CommonDesktopDirectory] = "/mock/CommonDesktop";

            // Act
            IEnumerator routine = (IEnumerator)_parser.GetType()
                .GetMethod("ParseShortcuts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_parser, null);

            while (routine.MoveNext()) yield return null;

            // Assert
            Assert.IsTrue(_parser.parsingComplete, "Parsing should be marked as complete.");
            Assert.AreEqual(0, _parser.shortcuts.Count, "Shortcuts list should be empty.");
        }

        [Test]
        public void AddShortcut_PopulatesList()
        {
            // Arrange
            string name = "TestApp";
            string path = "C:/test.exe";

            // Act
            _parser.AddShortcut(name, path, null);

            // Assert
            Assert.AreEqual(1, _parser.shortcuts.Count, "Should have 1 shortcut in the list.");
            Assert.AreEqual(name, _parser.shortcuts[0].Name, "Name should match.");
            Assert.AreEqual(path, _parser.shortcuts[0].TargetPath, "Path should match.");
        }

        [Test]
        public void ExtractHighQualityIcon_WhenCacheExists_ReturnsTexture()
        {
            // Arrange
            string filePath = "C:/Apps/MyGame.exe";
            string cachePath = Path.Combine(Application.dataPath, "Icons", "MyGame_icon.png");
            
            // Create a 1x1 PNG for the mock to return
            Texture2D dummyTex = new Texture2D(1, 1);
            byte[] pngData = dummyTex.EncodeToPNG();
            
            _mockFS.Files[cachePath] = pngData;

            // Act - Use reflection to call private method
            var method = _parser.GetType().GetMethod("ExtractHighQualityIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Texture2D result = (Texture2D)method.Invoke(_parser, new object[] { filePath });

            // Assert
            Assert.IsNotNull(result, "Should return a texture from cache.");
            Assert.AreEqual(1, result.width);
            Assert.AreEqual(1, result.height);
        }

        [UnityTest]
        public IEnumerator ParseShortcuts_WhenValidLnkFound_AddsShortcut()
        {
            // Arrange
            string desktop = "/mock/Desktop";
            _mockFS.Directories.Add(desktop);
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.Desktop] = desktop;
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.CommonDesktopDirectory] = "/mock/Empty";

            // Create a mock .lnk file that points to a valid .exe
            string lnkPath = Path.Combine(desktop, "MyApp.lnk");
            string targetExe = "C:/Apps/MyApp.exe";
            
            // Build a .lnk with real LinkInfo containing the target path
            byte[] lnkBytes = BuildLnkWithTargetPath(targetExe);
            _mockFS.Files[lnkPath] = lnkBytes;
            _mockFS.Files[targetExe] = new byte[0]; // Mark exe as existing

            // Act
            IEnumerator routine = (IEnumerator)_parser.GetType()
                .GetMethod("ParseShortcuts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_parser, null);

            while (routine.MoveNext()) yield return null;

            // Assert
            Assert.IsTrue(_parser.parsingComplete);
            Assert.AreEqual(1, _parser.shortcuts.Count, "Should have added 1 shortcut.");
            Assert.AreEqual("MyApp", _parser.shortcuts[0].Name);
            Assert.AreEqual(targetExe, _parser.shortcuts[0].TargetPath);
        }

        [UnityTest]
        public IEnumerator ParseShortcuts_WhenTargetMissing_SkipsShortcut()
        {
            // Arrange
            string desktop = "/mock/Desktop";
            _mockFS.Directories.Add(desktop);
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.Desktop] = desktop;
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.CommonDesktopDirectory] = "/mock/Empty";

            string lnkPath = Path.Combine(desktop, "Missing.lnk");
            string targetExe = "C:/Apps/Missing.exe";
            byte[] lnkBytes = BuildLnkWithTargetPath(targetExe);
            _mockFS.Files[lnkPath] = lnkBytes;
            // Note: targetExe NOT added to _mockFS.Files

            // Act
            IEnumerator routine = (IEnumerator)_parser.GetType()
                .GetMethod("ParseShortcuts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_parser, null);

            while (routine.MoveNext()) yield return null;

            // Assert
            Assert.AreEqual(0, _parser.shortcuts.Count, "Should have skipped shortcut with missing target.");
        }

        [Test]
        public void ExtractHighQualityIcon_WhenCacheCorrupted_DestroysTextureAndReturnsFallback()
        {
            // Arrange
            string filePath = "C:/Apps/Corrupt.exe";
            string cachePath = Path.Combine(Application.dataPath, "Icons", "Corrupt_icon.png");
            
            // Provide invalid image data (just some random bytes that aren't a PNG)
            _mockFS.Files[cachePath] = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            // Act
            var method = _parser.GetType().GetMethod("ExtractHighQualityIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Texture2D result = (Texture2D)method.Invoke(_parser, new object[] { filePath });

            // Assert
            // It should return null because extraction will fail (no real exe at that path)
            Assert.IsNull(result, "Should return null because extraction fails after cache load failure.");
        }

        private static byte[] BuildLnkWithTargetPath(string targetPath)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Header (76 bytes)
                writer.Write(76); // HeaderSize
                writer.Write(new byte[16]); // CLSID
                writer.Write(0x02); // LinkFlags (HasLinkInfo)
                writer.Write(0); // FileAttributes
                writer.Write(new byte[36]); // Timestamps
                writer.Write((ushort)0); // HotKey
                writer.Write(new byte[10]); // Reserved

                // LinkInfo section
                byte[] pathBytes = System.Text.Encoding.Default.GetBytes(targetPath + "\0");
                int headerSize = 28;
                int totalSize = headerSize + pathBytes.Length;

                writer.Write(totalSize); // LinkInfoSize
                writer.Write(headerSize); // LinkInfoHeaderSize
                writer.Write(1); // LinkInfoFlags (VolumeIDAndLocalBasePath)
                writer.Write(0); // VolumeIDOffset
                writer.Write(headerSize); // LocalBasePathOffset
                writer.Write(0); // CommonNetworkRelativeLinkOffset
                writer.Write(0); // CommonPathSuffixOffset

                writer.Write(pathBytes);

                return ms.ToArray();
            }
        }
    }
}

