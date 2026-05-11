using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class SystemButtonsController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Button exitButton;
    private Button refreshButton;

    void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        // Use a small delay to ensure the UI has actually cloned the tree
        uiDocument.rootVisualElement.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            InitializeButtons();
        });
    }

    private void InitializeButtons()
    {
        var root = uiDocument.rootVisualElement;

        // Link and unregister first to prevent "double-clicking" bugs
        exitButton = root.Q<Button>("exit");
        refreshButton = root.Q<Button>("refresh");

        if (exitButton != null)
        {
            exitButton.clicked -= OnExitClicked; // Safety: remove old listeners
            exitButton.clicked += OnExitClicked;
            Debug.Log("Exit Button Bound!");
        }

        if (refreshButton != null)
        {
            refreshButton.clicked -= OnRefreshClicked;
            refreshButton.clicked += OnRefreshClicked;
            Debug.Log("Refresh Button Bound!");
        }
    }

    private void OnExitClicked()
    {
        Debug.Log("Exiting Mock OS...");

        // This works in a standalone build
        Application.Quit();

        // This allows the button to work inside the Unity Editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnRefreshClicked()
    {
        Debug.Log("Restarting Session...");

        // Gets the currently active scene and reloads it
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }
}