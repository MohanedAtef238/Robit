using UnityEngine;
using System;

public class UnityNativeBridge : MonoBehaviour
{
    // Guarded accessor — throws with actionable message rather than NullReferenceException
    // if the singleton is accessed before Awake() or after OnDestroy().
    public static NativeLifetimeCoordinator Coordinator
    {
        get
        {
            if (_instance == null)
                throw new InvalidOperationException("UnityNativeBridge is not initialized. Ensure it is in the scene and Awake() has run.");
            return _instance._coordinator;
        }
    }

    private static UnityNativeBridge _instance;
    private NativeLifetimeCoordinator _coordinator;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _coordinator = new NativeLifetimeCoordinator();
        DontDestroyOnLoad(gameObject);
        RobitLogger.Log("[UnityNativeBridge] Coordinator initialized.");
    }

    private void OnApplicationQuit()
    {
        // Start the drain phase: prevent new callbacks and wait for in-flight ones.
        _coordinator?.BeginShutdown();
        Win32AudioInterop.Shutdown();
        RobitLogger.Log("[UnityNativeBridge] Native resources drained for shutdown.");
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _coordinator?.Dispose();
            _coordinator = null;
            _instance = null;
        }
    }
}
