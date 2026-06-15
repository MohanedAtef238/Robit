using NUnit.Framework;
using System.IO;

namespace Robit.Tests
{
    public class PhysicalFileSystemTests
    {
        private PhysicalFileSystem _fileSystem;
        private string _testDirectory;
        private string _testFile;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new PhysicalFileSystem();
            
            // Create a temporary directory and file for testing
            _testDirectory = Path.Combine(Path.GetTempPath(), "Robit_PhysicalFileSystemTest");
            if (!Directory.Exists(_testDirectory))
            {
                Directory.CreateDirectory(_testDirectory);
            }

            _testFile = Path.Combine(_testDirectory, "test_file.txt");
            File.WriteAllText(_testFile, "Hello World");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the temporary files from the host PC
            if (File.Exists(_testFile))
            {
                File.Delete(_testFile);
            }
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Test]
        [Category("Integration")]
        public void DirectoryExists_ReturnsTrue_ForRealDirectory()
        {
            Assert.IsTrue(_fileSystem.DirectoryExists(_testDirectory));
        }

        [Test]
        [Category("Integration")]
        public void DirectoryExists_ReturnsFalse_ForFakeDirectory()
        {
            Assert.IsFalse(_fileSystem.DirectoryExists(_testDirectory + "_fake"));
        }

        [Test]
        [Category("Integration")]
        public void FileExists_ReturnsTrue_ForRealFile()
        {
            Assert.IsTrue(_fileSystem.FileExists(_testFile));
        }

        [Test]
        [Category("Integration")]
        public void FileExists_ReturnsFalse_ForFakeFile()
        {
            Assert.IsFalse(_fileSystem.FileExists(_testFile + "_fake"));
        }

        [Test]
        [Category("Integration")]
        public void GetFiles_FindsActualFilesInDirectory()
        {
            string[] files = _fileSystem.GetFiles(_testDirectory, "*.txt", false);
            Assert.AreEqual(1, files.Length);
            Assert.AreEqual(_testFile, files[0]);
        }

        [Test]
        [Category("Integration")]
        public void ReadAllBytes_ReadsActualFileContent()
        {
            byte[] data = _fileSystem.ReadAllBytes(_testFile);
            string text = System.Text.Encoding.UTF8.GetString(data);
            Assert.AreEqual("Hello World", text);
        }

        [Test]
        [Category("Integration")]
        public void GetSpecialFolderPath_ReturnsNonEmptyString()
        {
            string desktopPath = _fileSystem.GetSpecialFolderPath(System.Environment.SpecialFolder.Desktop);
            Assert.IsFalse(string.IsNullOrEmpty(desktopPath), "Desktop path should not be empty on Windows.");
            Assert.IsTrue(Directory.Exists(desktopPath), "Returned path should be a valid existing directory.");
        }
    }
}
