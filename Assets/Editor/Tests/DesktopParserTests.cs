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
            Assert.IsTrue(_parser.IsValidShortcut("test.exe"));
            Assert.IsTrue(_parser.IsValidShortcut("TEST.EXE"));
            Assert.IsFalse(_parser.IsValidShortcut("test.txt"));
            Assert.IsFalse(_parser.IsValidShortcut(""));
            Assert.IsFalse(_parser.IsValidShortcut(null));
        }

        [UnityTest]
        public IEnumerator ParseShortcuts_HandlesEmptyDirectories()
        {
            // Setup mock: directories exist but are empty
            _mockFS.Directories.Add("/mock/StartMenu");
            _mockFS.Directories.Add("/mock/CommonStartMenu");
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.StartMenu] = "/mock/StartMenu";
            _mockFS.SpecialFolders[System.Environment.SpecialFolder.CommonStartMenu] = "/mock/CommonStartMenu";

            // Run the coroutine manually
            IEnumerator routine = (IEnumerator)_parser.GetType()
                .GetMethod("ParseShortcuts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_parser, null);

            while (routine.MoveNext()) yield return null;

            Assert.IsTrue(_parser.parsingComplete);
            Assert.AreEqual(0, _parser.shortcuts.Count);
        }

        [Test]
        public void AddShortcut_PopulatesList()
        {
            _parser.AddShortcut("TestApp", "C:/test.exe", null);
            Assert.AreEqual(1, _parser.shortcuts.Count);
            Assert.AreEqual("TestApp", _parser.shortcuts[0].Name);
            Assert.AreEqual("C:/test.exe", _parser.shortcuts[0].TargetPath);
        }
    }
}
