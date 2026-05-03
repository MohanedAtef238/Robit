using System;

namespace Robit.LauncherSystem
{
    public interface IProcess : IDisposable
    {
        int Id { get; }
        string ProcessName { get; }
        bool HasExited { get; }
        void Kill();
        void WaitForExit();
    }
}
