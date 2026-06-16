using NUnit.Framework;

namespace Robit.Tests
{
    public class MacroViewModelTests
    {
        [Test]
        [Category("Unit")]
        public void Open_ChangesStateAndFiresEvent()
        {
            var vm = new MacroViewModel();
            bool eventFired = false;
            vm.OnMenuToggled += (state) => eventFired = state;

            Assert.IsFalse(vm.IsOpen);
            vm.Open();
            Assert.IsTrue(vm.IsOpen);
            Assert.IsTrue(eventFired);
        }

        [Test]
        [Category("Unit")]
        public void Close_ChangesStateAndFiresEvent()
        {
            var vm = new MacroViewModel();
            vm.Open();
            
            bool eventFired = true;
            vm.OnMenuToggled += (state) => eventFired = state;

            vm.Close();
            Assert.IsFalse(vm.IsOpen);
            Assert.IsFalse(eventFired);
        }

        [Test]
        [Category("Unit")]
        public void NextGroup_CyclesForward()
        {
            var vm = new MacroViewModel();
            vm.Open();
            
            int currentIndex = vm.CurrentGroupIndex;
            vm.NextGroup();
            Assert.AreEqual(currentIndex + 1, vm.CurrentGroupIndex);
        }

        [Test]
        [Category("Unit")]
        public void NextGroup_WrapsAroundAtEnd()
        {
            var vm = new MacroViewModel();
            vm.Open();
            
            // Advance to the end
            for (int i = 0; i < MacroViewModel.Groups.Length; i++)
            {
                vm.NextGroup();
            }
            
            // Because we advanced exactly 'Length' times, it should have wrapped back to 0
            Assert.AreEqual(0, vm.CurrentGroupIndex);
        }

        [Test]
        [Category("Unit")]
        public void PrevGroup_CyclesBackwardAndWraps()
        {
            var vm = new MacroViewModel();
            vm.Open();
            
            // Going backwards from 0 should wrap to Length - 1
            vm.PrevGroup();
            Assert.AreEqual(MacroViewModel.Groups.Length - 1, vm.CurrentGroupIndex);
        }

        [Test]
        [Category("Unit")]
        public void PrevGroup_And_NextGroup_DoNothingWhenClosed()
        {
            var vm = new MacroViewModel();
            Assert.IsFalse(vm.IsOpen);
            
            vm.NextGroup();
            Assert.AreEqual(0, vm.CurrentGroupIndex); // Remains 0
            
            vm.PrevGroup();
            Assert.AreEqual(0, vm.CurrentGroupIndex); // Remains 0
        }

        [Test]
        [Category("Unit")]
        public void GetCurrentGroup_ReturnsCorrectGroup()
        {
            var vm = new MacroViewModel();
            vm.Open();
            
            var group = vm.GetCurrentGroup();
            Assert.AreEqual(MacroViewModel.Groups[0].name, group.name);
            
            vm.NextGroup();
            group = vm.GetCurrentGroup();
            Assert.AreEqual(MacroViewModel.Groups[1].name, group.name);
        }
    }
}
