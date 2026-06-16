using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TMPro;

/// <summary>
/// Drives the Starting Page scene.
/// Spawns profile cards, handles name/delete prompts, and runs the custom loading overlay with an existing scene firefly.
/// </summary>
public class StartingPageController : MonoBehaviour
{
    [Header("Prefabs")]
    public ProfileCard profileCardPrefab;
    public ProfileCard plusCardPrefab;

    [Header("Scene References")]
    public Transform profilesContainer;       // HorizontalLayoutGroup parent
    public TextMeshProUGUI welcomeText;

    [Header("Name Prompt")]
    public GameObject namePromptPanel;
    public TMP_InputField nameInput;
    public UnityEngine.UI.Button btnCreate;
    public UnityEngine.UI.Button btnCancel;
    public UnityEngine.UI.Image bgImageRef; // kept explicit to avoid future ambiguity

    [Header("Intro Animation")]
    public float introDelay = 0.3f;           // pause before animation starts
    public float animDuration = 1.4f;         // how long the float-up takes

    [Header("Custom Loading Customizer")]
    [Tooltip("Drag your loading screen background texture/sprite asset here.")]
    public Sprite loadingBackgroundSprite;

    [Tooltip("Drag your foreground overlay image/sprite asset here.")]
    public Sprite loadingOverlaySprite;

    [Tooltip("Set the explicit pixel size (Width, Height) for your overlay image. Leave at (0,0) to stretch full-screen.")]
    public Vector2 overlaySize = new Vector2(0f, 0f);

    [Tooltip("Controls the repeating pattern density of your tiled image overlay.")]
    [Range(0.01f, 10f)] public float pixelsPerUnitMultiplier = 1f;

    [Tooltip("DRAG your existing Scene Firefly GameObject straight from the hierarchy into this slot.")]
    public GameObject activeFirefly;

    [Header("Firefly Flight Settings")]
    [Tooltip("The movement speed of the firefly across the screen.")]
    public float flySpeed = 5f;
    [Tooltip("How fast the firefly rotates toward its flight direction.")]
    public float turnSpeed = 10f;
    [Tooltip("Scale size modifier for the firefly object.")]
    public Vector3 flyScale = new Vector3(5f, 5f, 5f);
    [Tooltip("Screen boundaries for random movement (X and Y limits).")]
    public Vector2 movementBounds = new Vector2(8f, 4.5f);
    [Tooltip("Distance threshold to consider a waypoint reached.")]
    public float waypointReachedDistance = 0.5f;

    [Header("Debug Settings")]
    [Tooltip("Minimum time in seconds the loading screen will stay visible for testing.")]
    public float minimumLoadingTime = 5f;

    private ProfileData _data;
    private GameObject loadingOverlay;

    // UI Toolkit Prompts
    private UIDocument _modernPromptDoc;
    private TextField _modernNameInput;
    private UIDocument _modernDeleteDoc;
    private string _profileToDeleteId;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    void Start()
    {
        namePromptPanel.SetActive(false);
        btnCreate.onClick.AddListener(OnCreateConfirmed);
        btnCancel.onClick.AddListener(HidePrompt);

        if (welcomeText != null)
        {
            welcomeText.text = "Robit";
        }

        // Dynamically create the custom layout layers inside the active UI Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            loadingOverlay = new GameObject("CustomLoadingOverlay");
            loadingOverlay.transform.SetParent(canvas.transform, false);

            RectTransform rtRoot = loadingOverlay.AddComponent<RectTransform>();
            rtRoot.anchorMin = Vector2.zero;
            rtRoot.anchorMax = Vector2.one;
            rtRoot.offsetMin = Vector2.zero;
            rtRoot.offsetMax = Vector2.zero;

            // 1. Custom Background Layer
            GameObject bg = new GameObject("LoadingBackground");
            bg.transform.SetParent(loadingOverlay.transform, false);
            RectTransform rtBg = bg.AddComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;

            UnityEngine.UI.Image bgImage = bg.AddComponent<UnityEngine.UI.Image>();
            if (loadingBackgroundSprite != null)
            {
                bgImage.sprite = loadingBackgroundSprite;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            }

            // 2. UI Component TILED Overlay Layer
            if (loadingOverlaySprite != null)
            {
                GameObject overlayImgObj = new GameObject("LoadingOverlayImage");
                overlayImgObj.transform.SetParent(loadingOverlay.transform, false);
                RectTransform rtOverlay = overlayImgObj.AddComponent<RectTransform>();

                if (overlaySize.x > 0.001f && overlaySize.y > 0.001f)
                {
                    rtOverlay.anchorMin = new Vector2(0.5f, 0.5f);
                    rtOverlay.anchorMax = new Vector2(0.5f, 0.5f);
                    rtOverlay.pivot = new Vector2(0.5f, 0.5f);
                    rtOverlay.sizeDelta = overlaySize;
                }
                else
                {
                    rtOverlay.anchorMin = Vector2.zero;
                    rtOverlay.anchorMax = Vector2.one;
                    rtOverlay.offsetMin = Vector2.zero;
                    rtOverlay.offsetMax = Vector2.zero;
                }

                UnityEngine.UI.Image overlayImage = overlayImgObj.AddComponent<UnityEngine.UI.Image>();
                overlayImage.sprite = loadingOverlaySprite;

                // Forces it to act as a tiled runtime UI asset
                overlayImage.type = UnityEngine.UI.Image.Type.Tiled;

                // NEW: Applies your custom slider scale to the tiled pattern generation
                overlayImage.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;

                overlayImage.color = Color.white;
            }

            // 3. Setup Scene Firefly Component Control Logic
            if (activeFirefly != null)
            {
                activeFirefly.transform.localScale = flyScale;

                SmoothFireflyMover mover = activeFirefly.GetComponent<SmoothFireflyMover>();
                if (mover == null)
                {
                    mover = activeFirefly.AddComponent<SmoothFireflyMover>();
                }

                mover.moveSpeed = flySpeed;
                mover.rotationSpeed = turnSpeed;
                mover.movementBounds = movementBounds;
                mover.waypointReachedDistance = waypointReachedDistance;

                activeFirefly.SetActive(false);
            }
            else
            {
                UnityEngine.Debug.LogWarning("No Scene Firefly assigned in the controller slot!");
            }

            loadingOverlay.SetActive(false);
        }

        _data = ProfileManager.LoadProfiles();
        StartCoroutine(PlayIntro());
    }

    // ── Intro animation ─────────────────────────────────────────────────────

    private IEnumerator PlayIntro()
    {
        RectTransform rt = welcomeText.rectTransform;

        Vector2 restPos = new Vector2(0f, -80f);
        Vector2 startPos = new Vector2(0f, -480f);
        float startScale = 1.4f;
        float endScale = 1.0f;

        rt.anchoredPosition = startPos;
        welcomeText.transform.localScale = Vector3.one * startScale;

        yield return new WaitForSeconds(introDelay);

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            rt.anchoredPosition = Vector2.Lerp(startPos, restPos, t);
            welcomeText.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = restPos;
        welcomeText.transform.localScale = Vector3.one * endScale;

        RebuildCards(true);
    }

    // ── Card rendering ──────────────────────────────────────────────────────

    private void RebuildCards(bool animate = false)
    {
        foreach (Transform child in profilesContainer)
            Destroy(child.gameObject);

        float popDelay = 0f;

        foreach (UserProfile profile in _data.profiles)
        {
            Sprite avatar = LoadAvatar(profile.pfpPath);
            ProfileCard card = Instantiate(profileCardPrefab, profilesContainer);
            card.Setup(profile, avatar,
                () => {
                    if (profile.isCalibrated) SelectProfile(profile.id);
                    else RecalibrateProfile(profile.id);
                },
                () => RecalibrateProfile(profile.id),
                () => DeleteProfile(profile.id)
            );

            if (animate)
            {
                card.AnimatePopIn(popDelay);
                popDelay += 0.15f;
            }
        }

        if (_data.profiles.Count < ProfileManager.MaxProfiles)
        {
            ProfileCard plus = Instantiate(plusCardPrefab, profilesContainer);
            plus.SetupAsPlus(ShowPrompt);

            if (animate) plus.AnimatePopIn(popDelay);
        }
    }

    private Sprite LoadAvatar(string pfpPath)
    {
        if (!string.IsNullOrEmpty(pfpPath))
        {
            Sprite s = Resources.Load<Sprite>($"ProfilePictures/{pfpPath}");
            if (s != null) return s;
        }
        Sprite[] all = Resources.LoadAll<Sprite>("ProfilePictures");
        return all.Length > 0 ? all[0] : null;
    }

    // ── Profile selection & Navigation ───────────────────────────────────────

    private void RecalibrateProfile(string profileId)
    {
        PlayerPrefs.SetString("ActiveProfileID", profileId);
        PlayerPrefs.Save();

        StartCoroutine(ExecuteLoadingFlowWithDelay(profileId, isCalibrationFlow: true));
    }

    private void SelectProfile(string profileId)
    {
        PlayerPrefs.SetString("ActiveProfileID", profileId);
        PlayerPrefs.Save();

        StartCoroutine(ExecuteLoadingFlowWithDelay(profileId, isCalibrationFlow: false));
    }

    private IEnumerator ExecuteLoadingFlowWithDelay(string profileId, bool isCalibrationFlow)
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(true);
        }
        else if (welcomeText != null)
        {
            welcomeText.text = "Loading...";
        }

        if (activeFirefly != null)
        {
            activeFirefly.SetActive(true);
            UnityEngine.Debug.Log($"Debug: Active Firefly set to TRUE at position {activeFirefly.transform.position}");
        }

        if (profilesContainer != null)
        {
            profilesContainer.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(minimumLoadingTime);

        var runner = FindFirstObjectByType<GazeFollowerRunner>();

        if (isCalibrationFlow)
        {
            if (runner != null)
            {
                runner.TriggerCalibrationFlow(profileId);
            }
            else
            {
                SceneManager.LoadScene("GazeCalibrationScene");
            }
        }
        else
        {
            if (runner != null)
            {
                runner.TriggerStartupFlow(profileId);
            }
            else
            {
                SceneManager.LoadScene("OverlayScene");
            }
        }
    }

    private void DeleteProfile(string profileId)
    {
        ShowDeletePrompt(profileId);
    }

    // ── Name prompt ──────────────────────────────────────────────────────────

    private void SetupModernPromptIfMissing()
    {
        if (_modernPromptDoc == null)
        {
            _modernPromptDoc = gameObject.AddComponent<UIDocument>();
            _modernPromptDoc.visualTreeAsset = Resources.Load<VisualTreeAsset>("ModernNamePrompt");
            _modernPromptDoc.panelSettings = Resources.Load<PanelSettings>("New Panel Settings");
            _modernPromptDoc.sortingOrder = 100;

            var root = _modernPromptDoc.rootVisualElement;
            root.style.display = DisplayStyle.None;

            _modernNameInput = root.Q<TextField>("input-name");
            var btnCreate = root.Q<Button>("btn-create");
            var btnCancel = root.Q<Button>("btn-cancel");

            if (btnCreate != null) btnCreate.clicked += OnCreateConfirmed;
            if (btnCancel != null) btnCancel.clicked += HidePrompt;
        }
    }

    private void ShowPrompt()
    {
        SetupModernPromptIfMissing();
        if (_modernNameInput != null) _modernNameInput.value = string.Empty;
        if (_modernPromptDoc != null) _modernPromptDoc.rootVisualElement.style.display = DisplayStyle.Flex;

        if (namePromptPanel != null) namePromptPanel.SetActive(false);
        OpenOnScreenKeyboard();
    }

    private void HidePrompt()
    {
        if (_modernPromptDoc != null) _modernPromptDoc.rootVisualElement.style.display = DisplayStyle.None;
        if (namePromptPanel != null) namePromptPanel.SetActive(false);
    }

    private void OnCreateConfirmed()
    {
        string trimmed = _modernNameInput != null ? _modernNameInput.value.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmed)) trimmed = "Profile " + (_data.profiles.Count + 1);

        _data.profiles.Add(ProfileManager.CreateProfile(trimmed, _data.profiles));
        ProfileManager.SaveProfiles(_data);

        HidePrompt();
        RebuildCards(false);
    }

    // ── Delete prompt ────────────────────────────────────────────────────────

    private void SetupDeletePromptIfMissing()
    {
        if (_modernDeleteDoc == null)
        {
            var go = new GameObject("DeletePromptDoc");
            go.transform.SetParent(this.transform);
            _modernDeleteDoc = go.AddComponent<UIDocument>();
            _modernDeleteDoc.visualTreeAsset = Resources.Load<VisualTreeAsset>("ModernDeletePrompt");
            _modernDeleteDoc.panelSettings = Resources.Load<PanelSettings>("New Panel Settings");
            _modernDeleteDoc.sortingOrder = 101;

            var root = _modernDeleteDoc.rootVisualElement;
            root.style.display = DisplayStyle.None;

            var btnConfirm = root.Q<Button>("btn-confirm");
            var btnCancel = root.Q<Button>("btn-cancel");

            if (btnConfirm != null) btnConfirm.clicked += OnDeleteConfirmed;
            if (btnCancel != null) btnCancel.clicked += HideDeletePrompt;
        }
    }

    private void ShowDeletePrompt(string profileId)
    {
        _profileToDeleteId = profileId;
        SetupDeletePromptIfMissing();
        if (_modernDeleteDoc != null) _modernDeleteDoc.rootVisualElement.style.display = DisplayStyle.Flex;
    }

    private void HideDeletePrompt()
    {
        if (_modernDeleteDoc != null) _modernDeleteDoc.rootVisualElement.style.display = DisplayStyle.None;
        _profileToDeleteId = null;
    }

    private void OnDeleteConfirmed()
    {
        if (!string.IsNullOrEmpty(_profileToDeleteId))
        {
            _data.profiles.RemoveAll(p => p.id == _profileToDeleteId);
            ProfileManager.SaveProfiles(_data);
            RebuildCards(false);
        }
        HideDeletePrompt();
    }

    private void OpenOnScreenKeyboard()
    {
        UnityEngine.Debug.Log("OnScreenKeyboard triggered (exe starters disabled).");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(movementBounds.x * 2, movementBounds.y * 2, 0.1f));
    }
}

/// <summary>
/// Handles wide, oriented flight paths for the existing 3D Scene Firefly locked completely to Z-axis rotation.
/// </summary>
public class SmoothFireflyMover : MonoBehaviour
{
    [HideInInspector] public float moveSpeed;
    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public Vector2 movementBounds;
    [HideInInspector] public float waypointReachedDistance;

    private Vector3 targetPosition;
    private float constantZDepth;

    void Start()
    {
        constantZDepth = transform.position.z;
        PickRandomTarget();
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        Vector3 direction = targetPosition - transform.position;
        direction.z = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, targetPosition) < waypointReachedDistance)
        {
            PickRandomTarget();
        }
    }

    private void PickRandomTarget()
    {
        float randomX = Random.Range(-movementBounds.x, movementBounds.x);
        float randomY = Random.Range(-movementBounds.y, movementBounds.y);

        targetPosition = new Vector3(randomX, randomY, constantZDepth);
    }
}