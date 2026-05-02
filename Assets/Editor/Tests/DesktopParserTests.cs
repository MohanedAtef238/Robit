using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace Tests.Editor
{
    public class DesktopParserTests
    {
        private GameObject _holder;
        private DesktopParser _parser;

        [SetUp]
        public void Setup()
        {
            // 1. Arrange (Common setup for all tests)
            _holder = new GameObject("TestParser");
            _parser = _holder.AddComponent<DesktopParser>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void IsValidShortcut_ReturnsTrue_ForExePath()
        {
            // 1. Arrange
            string validPath = "C:\\Windows\\System32\\calc.exe";

            // 2. Act
            bool result = _parser.IsValidShortcut(validPath);

            // 3. Assert
            Assert.IsTrue(result, "Should return true for .exe paths.");
        }

        [Test]
        public void IsValidShortcut_ReturnsFalse_ForNonExePath()
        {
            // 1. Arrange
            string invalidPath = "C:\\Users\\User\\Desktop\\Document.txt";

            // 2. Act
            bool result = _parser.IsValidShortcut(invalidPath);

            // 3. Assert
            Assert.IsFalse(result, "Should return false for non-.exe paths.");
        }

        [Test]
        public void IsValidShortcut_ReturnsFalse_ForEmptyPath()
        {
            // 1. Arrange
            string emptyPath = "";

            // 2. Act
            bool result = _parser.IsValidShortcut(emptyPath);

            // 3. Assert
            Assert.IsFalse(result, "Should return false for empty strings.");
        }

        [Test]
        public void AddShortcut_CorrectlyPopulatesList()
        {
            // 1. Arrange
            string name = "Test App";
            string path = "C:\\Test.exe";
            Texture2D mockIcon = null;

            // 2. Act
            _parser.AddShortcut(name, path, mockIcon);

            // 3. Assert
            Assert.AreEqual(1, _parser.shortcuts.Count, "Shortcuts list should have 1 item.");
            Assert.AreEqual(name, _parser.shortcuts[0].Name);
            Assert.AreEqual(path, _parser.shortcuts[0].TargetPath);
        }
        
        [Test]
        public void AddShortcut_HandlesMultipleEntries()
        {
            // 1. Arrange
            _parser.AddShortcut("App1", "path1", null);
            _parser.AddShortcut("App2", "path2", null);

            // 2. Act
            int count = _parser.shortcuts.Count;

            // 3. Assert
            Assert.AreEqual(2, count, "Shortcuts list should have 2 items.");
        }
    }
}
