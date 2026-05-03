using System;
using System.Collections.Generic;
using Robit.LauncherSystem;

public class MockProcess : IProcess
{
    public int    Id          => 123;
    public string ProcessName => "MockProcess";
    public bool   HasExited   { get; set; }
    public bool   KillCalled  { get; private set; }

    public void Kill()        => KillCalled = true;
    public void WaitForExit() { }
    public void Dispose()     { }
}

public class MockProcessRunner : IProcessRunner
{
    public string      LastStartedPath      { get; private set; }
    public string      LastWorkingDirectory { get; private set; }
    public bool        CloseCalled          { get; private set; }
    public bool        ShouldFail           { get; set; }
    public MockProcess LastStartedProcess   { get; private set; }

    public IProcess Start(string path, string workingDirectory)
    {
        if (ShouldFail)
            throw new Exception("Mock Launch Failure");

        LastStartedPath      = path;
        LastWorkingDirectory = workingDirectory;
        LastStartedProcess   = new MockProcess();
        return LastStartedProcess;
    }

    public void Close(IProcess process)
    {
        CloseCalled = true;
    }

    /// <summary>
    /// Resets tracking state between calls in multi-launch tests.
    /// Does NOT reset ShouldFail — set that explicitly in Arrange.
    /// </summary>
    public void Reset()
    {
        LastStartedPath      = null;
        LastWorkingDirectory = null;
        CloseCalled          = false;
        LastStartedProcess   = null;
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
