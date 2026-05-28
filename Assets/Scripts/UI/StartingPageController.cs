using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Starting Page scene.
/// Spawns profile cards, handles the name prompt, and plays the intro animation.
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
    public Button btnCreate;
    public Button btnCancel;

    [Header("Intro Animation")]
    public float introDelay = 0.3f;           // pause before animation starts
    public float animDuration = 1.4f;         // how long the float-up takes

    private ProfileData _data;
    private GameObject loadingOverlay;

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

        // Dynamically create the loading overlay using the Loading15 prefab
        GameObject prefab = Resources.Load<GameObject>("Loading15");
        if (prefab != null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                loadingOverlay = new GameObject("LoadingOverlay");
                loadingOverlay.transform.SetParent(canvas.transform, false);
                RectTransform rtRoot = loadingOverlay.AddComponent<RectTransform>();
                rtRoot.anchorMin = Vector2.zero;
                rtRoot.anchorMax = Vector2.one;
                rtRoot.offsetMin = Vector2.zero;
                rtRoot.offsetMax = Vector2.zero;

                // Dark Background
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(loadingOverlay.transform, false);
                RectTransform rtBg = bg.AddComponent<RectTransform>();
                rtBg.anchorMin = Vector2.zero;
                rtBg.anchorMax = Vector2.one;
                rtBg.offsetMin = Vector2.zero;
                rtBg.offsetMax = Vector2.zero;
                Image bgImage = bg.AddComponent<Image>();
                bgImage.color = new Color(0.04f, 0.05f, 0.08f, 1f); // Dark background

                // Spinner
                GameObject spinner = Instantiate(prefab, loadingOverlay.transform);
                RectTransform rtSpinner = spinner.GetComponent<RectTransform>();
                if (rtSpinner != null)
                {
                    rtSpinner.anchoredPosition = Vector2.zero;
                    rtSpinner.localScale = Vector3.one * 1.5f; // scale it slightly for better visibility
                }
                
                loadingOverlay.SetActive(false);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Loading15 prefab not found in Resources folder.");
        }

        _data = ProfileManager.LoadProfiles();

        StartCoroutine(PlayIntro());
    }

    // ── Intro animation ─────────────────────────────────────────────────────

    private IEnumerator PlayIntro()
    {
        RectTransform rt = welcomeText.rectTransform;

        // Rest position: -80px from top (set in Editor, we animate TO here)
        Vector2 restPos  = new Vector2(0f, -80f);
        // Start position: push it 400px further down so it begins near screen centre
        Vector2 startPos = new Vector2(0f, -480f);
        float startScale = 1.4f;
        float endScale   = 1.0f;

        rt.anchoredPosition              = startPos;
        welcomeText.transform.localScale = Vector3.one * startScale;

        yield return new WaitForSeconds(introDelay);

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            rt.anchoredPosition              = Vector2.Lerp(startPos, restPos, t);
            welcomeText.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition              = restPos;
        welcomeText.transform.localScale = Vector3.one * endScale;

        // Intro finished, now spawn and pop the cards
        RebuildCards(true);
    }

    // ── Card rendering ──────────────────────────────────────────────────────

    private void RebuildCards(bool animate = false)
    {
        // Clear existing cards
        foreach (Transform child in profilesContainer)
            Destroy(child.gameObject);

        float popDelay = 0f;

        foreach (UserProfile profile in _data.profiles)
        {
            Sprite avatar = LoadAvatar(profile.pfpPath);
            ProfileCard card = Instantiate(profileCardPrefab, profilesContainer);
            card.Setup(profile, avatar, () => SelectProfile(profile.id));
            
            if (animate)
            {
                card.AnimatePopIn(popDelay);
                popDelay += 0.15f; // Stagger the pop-in
            }
        }

        // Show Plus button only when below the max
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
        // Fallback: pick any available placeholder
        Sprite[] all = Resources.LoadAll<Sprite>("ProfilePictures");
        return all.Length > 0 ? all[0] : null;
    }

    // ── Profile selection ────────────────────────────────────────────────────

    private void SelectProfile(string profileId)
    {
        PlayerPrefs.SetString("ActiveProfileID", profileId);
        PlayerPrefs.Save();

        // Provide immediate visual feedback that a profile was selected
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(true);
        }
        else if (welcomeText != null)
        {
            welcomeText.text = "Loading...";
        }

        if (profilesContainer != null)
        {
            profilesContainer.gameObject.SetActive(false);
        }

        var runner = FindFirstObjectByType<GazeFollowerRunner>();
        if (runner != null)
        {
            runner.TriggerStartupFlow(profileId);
        }
        else
        {
            SceneManager.LoadScene("OverlayScene");
        }
    }

    // ── Name prompt ──────────────────────────────────────────────────────────

    private void ShowPrompt()
    {
        nameInput.text = string.Empty;
        namePromptPanel.SetActive(true);
        OpenOnScreenKeyboard();
    }

    private void HidePrompt()
    {
        namePromptPanel.SetActive(false);
    }

    private void OnCreateConfirmed()
    {
        string trimmed = nameInput.text.Trim();
        if (string.IsNullOrEmpty(trimmed)) trimmed = "Profile " + (_data.profiles.Count + 1);

        _data.profiles.Add(ProfileManager.CreateProfile(trimmed, _data.profiles));
        ProfileManager.SaveProfiles(_data);

        HidePrompt();
        RebuildCards(false); // No animation needed when adding a new one, it'll just appear
    }

    private void OpenOnScreenKeyboard()
    {
        // try   { Process.Start("tabtip.exe"); }
        // catch { try { Process.Start("osk.exe"); } catch { /* silent */ } }
        UnityEngine.Debug.Log("OnScreenKeyboard triggered (exe starters disabled).");
    }
}
