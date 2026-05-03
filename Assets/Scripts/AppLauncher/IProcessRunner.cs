using System.Diagnostics;

public interface IProcessRunner
{
    Process Start(string path, string workingDirectory);
    void Close(Process process);
}
