using System.Diagnostics;
using System.IO;

public class WindowsProcessRunner : IProcessRunner
{
    public Process Start(string path, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(path);
        if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        return Process.Start(startInfo);
    }

    public void Close(Process process)
    {
        if (process != null && !process.HasExited)
        {
            process.CloseMainWindow();
            process.Dispose();
        }
    }
}
