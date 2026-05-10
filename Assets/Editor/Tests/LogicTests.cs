using NUnit.Framework;
using System.Collections.Generic;

namespace Robit.Tests.Logic
{
    [TestFixture]
    public class PaginationLogicTests
    {
        private PaginationLogic _logic;

        [SetUp]
        public void Setup()
        {
            _logic = new PaginationLogic(10);
        }

        [Test]
        public void GetPageCount_ReturnsCorrectCount()
        {
            Assert.AreEqual(1, _logic.GetPageCount(0));
            Assert.AreEqual(1, _logic.GetPageCount(5));
            Assert.AreEqual(1, _logic.GetPageCount(10));
            Assert.AreEqual(2, _logic.GetPageCount(11));
            Assert.AreEqual(2, _logic.GetPageCount(20));
        }

        [Test]
        public void GetPageRange_ReturnsCorrectIndices()
        {
            // Page 0: [0, 10)
            var (start, end) = _logic.GetPageRange(25, 0);
            Assert.AreEqual(0, start);
            Assert.AreEqual(10, end);

            // Page 1: [10, 20)
            (start, end) = _logic.GetPageRange(25, 1);
            Assert.AreEqual(10, start);
            Assert.AreEqual(20, end);

            // Page 2: [20, 25)
            (start, end) = _logic.GetPageRange(25, 2);
            Assert.AreEqual(20, start);
            Assert.AreEqual(25, end);
        }

        [Test]
        public void CanChangePage_BoundaryChecks()
        {
            _logic.CurrentPage = 0;
            Assert.IsFalse(_logic.CanChangePage(-1, 25));
            Assert.IsTrue(_logic.CanChangePage(1, 25));

            _logic.CurrentPage = 2; // Last page (20-25)
            Assert.IsTrue(_logic.CanChangePage(-1, 25));
            Assert.IsFalse(_logic.CanChangePage(1, 25));
        }
    }

    [TestFixture]
    public class AppCardLogicTests
    {
        private AppCardLogic _logic;

        [SetUp]
        public void Setup()
        {
            _logic = new AppCardLogic();
        }

        [Test]
        public void GenerateCardModels_ConvertsShortcuts()
        {
            var shortcuts = new List<ShortcutInfo>
            {
                new ShortcutInfo { Name = "App1", TargetPath = "C:/App1.exe" },
                new ShortcutInfo { Name = "App2", TargetPath = "C:/App2.exe", Icon = null }
            };

            var result = _logic.GenerateCardModels(shortcuts);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("App1", result[0].Name);
            Assert.AreEqual("C:/App1.exe", result[0].TargetPath);
            Assert.IsFalse(result[1].HasIcon);
        }

        [Test]
        public void GenerateCardModels_HandlesNull()
        {
            var result = _logic.GenerateCardModels(null);
            Assert.AreEqual(0, result.Count);
        }
    }
}
