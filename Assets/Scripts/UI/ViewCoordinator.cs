using UnityEngine;

public class ViewCoordinator : MonoBehaviour
{
    public static ViewCoordinator Instance { get; private set; }

    private HomePageController _homePage;
    private AppLauncherUIToolkit _appLauncher;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("ViewCoordinator");
        go.AddComponent<ViewCoordinator>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Cache references to our two mutually exclusive full-screen views
        _homePage = Object.FindFirstObjectByType<HomePageController>();
        _appLauncher = Object.FindFirstObjectByType<AppLauncherUIToolkit>();
    }

    public void ToggleHome()
    {
        if (_homePage == null)
            _homePage = Object.FindFirstObjectByType<HomePageController>();

        if (_homePage == null) 
        {
            RobitLogger.LogWarning("[ViewCoordinator] HomePageController not found.");
            return;
        }

        if (_homePage.IsOpen)
        {
            _homePage.Close();
        }
        else
        {
            // Dynamically find AppLauncher if it wasn't cached, since Start() runs before the scene loads.
            if (_appLauncher == null)
                _appLauncher = Object.FindFirstObjectByType<AppLauncherUIToolkit>();

            // Close the App Cycler first (synchronously sets _isOpen=false).
            // Its HidePanel coroutine will detect that the Home Page is now open
            // and will skip the SetAcrylicBlur(false) call — preventing the stomp.
            if (_appLauncher != null && _appLauncher.IsOpen)
                _appLauncher.Close();

            // HomePageController.Open() handles all window state internally:
            // PausePolling, SetAcrylicBlur(true), SetClickThrough(false), FocusWindow.
            _homePage.Open();
        }
    }


    public void ToggleAppCycler()
    {
        if (_appLauncher == null)
            _appLauncher = Object.FindFirstObjectByType<AppLauncherUIToolkit>();

        if (_appLauncher == null) 
        {
            RobitLogger.LogWarning("[ViewCoordinator] AppLauncherUIToolkit not found.");
            return;
        }

        bool isOpening = !_appLauncher.IsOpen;

        // Dynamically find HomePage if it wasn't cached, since Start() runs before the scene loads.
        if (_homePage == null)
            _homePage = Object.FindFirstObjectByType<HomePageController>();

        // If we are about to open the App Cycler, enforce mutual exclusivity
        if (isOpening && _homePage != null && _homePage.IsOpen)
        {
            _homePage.Close();
        }

        _appLauncher.Toggle();
    }
}
