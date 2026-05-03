using System.Diagnostics;
using System.Collections.Generic;
using Robit.LauncherSystem;

public class MockProcessRunner : IProcessRunner
{
    public string LastStartedPath { get; private set; }
    public string LastWorkingDirectory { get; private set; }
    public bool CloseCalled { get; private set; }
    public bool ShouldFail { get; set; }

    public IProcess Start(string path, string workingDirectory)
    {
        if (ShouldFail) throw new System.Exception("Mock Launch Failure");
        LastStartedPath = path;
        LastWorkingDirectory = workingDirectory;
        return null; // We return null because we don't want to spawn a real process
    }

    public void Close(IProcess process)
    {
        CloseCalled = true;
    }
}

public class MockSceneLoader : ISceneLoader
{
    public string LastLoadedScene { get; private set; }

    public void LoadScene(string sceneName)
    {
        LastLoadedScene = sceneName;
    }
}

