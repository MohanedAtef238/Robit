namespace Robit.LauncherSystem
{
    public interface IProcessRunner
    {
        IProcess Start(string path, string workingDirectory);
        void Close(IProcess process);
    }
}

