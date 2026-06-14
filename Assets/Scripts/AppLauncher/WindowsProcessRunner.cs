using System.Diagnostics;
using System.IO;

namespace Robit.LauncherSystem
{
    public class WindowsProcessRunner : IProcessRunner
    {
        public IProcess Start(string path, string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(path);
            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            Process p = Process.Start(startInfo);
            return p != null ? new WindowsProcess(p) : null;
        }

        public void Close(IProcess process)
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
                process.Dispose();
            }
        }
    }
}

