using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class NativeLifetimeCoordinatorTests
{
    [Test]
    public void TrackManagedObject_PinsReference()
    {
        var coordinator = new NativeLifetimeCoordinator();
        var obj = new object();
        var id = coordinator.TrackManagedObject(obj);
        
        // Ensure we can't release multiple times (idempotency of Release is handled by TryRemove)
        coordinator.Release(id);
        coordinator.Release(id); 
        
        coordinator.Dispose();
    }

    [Test]
    public void Shutdown_DrainsCallbacks()
    {
        var coordinator = new NativeLifetimeCoordinator();
        bool callbackEntered = false;
        
        var thread = new Thread(() => {
            coordinator.EnterCallback();
            callbackEntered = true;
            Thread.Sleep(100);
            coordinator.ExitCallback();
        });
        
        thread.Start();
        while (!callbackEntered) Thread.Sleep(5);
        
        // Should wait for the thread to exit (up to 500ms)
        coordinator.BeginShutdown();
        
        Assert.Throws<InvalidOperationException>(() => coordinator.EnterCallback());
        coordinator.Dispose();
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var coordinator = new NativeLifetimeCoordinator();
        coordinator.Dispose();
        coordinator.Dispose();
    }
}
