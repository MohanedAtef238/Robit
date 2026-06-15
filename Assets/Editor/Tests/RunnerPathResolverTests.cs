using NUnit.Framework;
using System.IO;
using System.Collections.Generic;

namespace Robit.Tests
{
    public class RunnerPathResolverTests
    {
        private string _testDirectory;
        private string _envFile;
        private string _targetFile;
        private string _targetDir;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "Robit_RunnerPathResolverTest");
            if (!Directory.Exists(_testDirectory))
            {
                Directory.CreateDirectory(_testDirectory);
            }

            _targetDir = Path.Combine(_testDirectory, "TargetFolder");
            Directory.CreateDirectory(_targetDir);

            _envFile = Path.Combine(_testDirectory, "test.env");
            File.WriteAllLines(_envFile, new string[]
            {
                "# This is a comment",
                "",
                "PYTHON_PATH=C:/python/python.exe",
                "MODEL_DIR=\"C:/models\"",
                "USE_GPU='true'",
                "  SPACED_KEY  =  spaced_value  "
            });

            _targetFile = Path.Combine(_testDirectory, "target.exe");
            File.WriteAllText(_targetFile, "dummy exe");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Test]
        [Category("Integration")]
        public void ParseEnvFile_ParsesCorrectly_IgnoringCommentsAndWhitespace()
        {
            var env = RunnerPathResolver.ParseEnvFile(_envFile);

            Assert.AreEqual(4, env.Count);
            
            // Basic key-value
            Assert.IsTrue(env.ContainsKey("PYTHON_PATH"));
            Assert.AreEqual("C:/python/python.exe", env["PYTHON_PATH"]);

            // Double quotes stripped
            Assert.IsTrue(env.ContainsKey("MODEL_DIR"));
            Assert.AreEqual("C:/models", env["MODEL_DIR"]);

            // Single quotes stripped
            Assert.IsTrue(env.ContainsKey("USE_GPU"));
            Assert.AreEqual("true", env["USE_GPU"]);

            // Spaces trimmed
            Assert.IsTrue(env.ContainsKey("SPACED_KEY"));
            Assert.AreEqual("spaced_value", env["SPACED_KEY"]);
        }

        [Test]
        [Category("Integration")]
        public void TryResolveEnvFilePath_ResolvesRootedPath()
        {
            // Path.Combine(UnityDir, AbsolutePath) collapses to AbsolutePath.
            // This tests that absolute paths are correctly identified.
            bool found = RunnerPathResolver.TryResolveEnvFilePath(_envFile, out string envPath, out string details);
            
            Assert.IsTrue(found);
            Assert.AreEqual(_envFile, envPath);
            Assert.IsNotEmpty(details);
        }

        [Test]
        [Category("Integration")]
        public void TryResolvePath_ResolvesRootedFile()
        {
            bool found = RunnerPathResolver.TryResolvePath(_targetFile, expectFile: true, out string resolved, out string details);
            
            Assert.IsTrue(found);
            Assert.AreEqual(_targetFile, resolved);
        }

        [Test]
        [Category("Integration")]
        public void TryResolvePath_ResolvesRootedDirectory()
        {
            bool found = RunnerPathResolver.TryResolvePath(_targetDir, expectFile: false, out string resolved, out string details);
            
            Assert.IsTrue(found);
            Assert.AreEqual(_targetDir, resolved);
        }

        [Test]
        [Category("Unit")]
        public void TryResolvePath_FailsGracefully_OnEmptyString()
        {
            bool found = RunnerPathResolver.TryResolvePath("   ", expectFile: true, out string resolved, out string details);
            
            Assert.IsFalse(found);
            Assert.IsNull(resolved);
            Assert.IsTrue(details.Contains("empty"));
        }
    }
}
