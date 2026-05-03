using System.Diagnostics;

namespace Robit.LauncherSystem
{
    public class WindowsProcess : IProcess
    {
        private readonly Process _process;

        public WindowsProcess(Process process)
        {
            _process = process;
        }

        public int Id => _process.Id;
        public string ProcessName => _process.ProcessName;
        public bool HasExited => _process.HasExited;

        public void Kill() => _process.Kill();
        public void WaitForExit() => _process.WaitForExit();

        public void Dispose() => _process.Dispose();
    }
}
