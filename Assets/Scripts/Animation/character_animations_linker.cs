using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the same GameObject as your Animator.
/// Handles all animation trigger logic:
///   - Intro sequence (melt crossfading into wave) on scene start
///   - Random idle animations on a timer
///   - Eye bulge on click        (wire ClickableObject.OnObjectClicked → OnCharacterClicked)
///   - Huh on mouse hold         (wire ClickableObject.OnObjectHeld    → OnCharacterHeld)
///   - Talking loop while inputDialogueField is focused
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimatorController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("UIDocument that contains the dialogue input field.")]
    [SerializeField] private UIDocument _uiDocument;

    [Tooltip("Name of the TextField in the UIDocument to watch for focus.")]
    [SerializeField] private string _dialogueFieldName = "inputDialogueField";

    [Header("Intro Sequence")]
    [Tooltip("Delay before the intro starts (seconds).")]
    [SerializeField] private float _introDelay = 0.5f;

    [Tooltip("How long after melt starts before wave crossfades in (seconds).")]
    [SerializeField] private float _meltToWaveDelay = 0.8f;

    [Header("Random Animations")]
    [Tooltip("Minimum seconds between random animation triggers.")]
    [SerializeField] private float _randomIntervalMin = 4f;

    [Tooltip("Maximum seconds between random animation triggers.")]
    [SerializeField] private float _randomIntervalMax = 10f;

    [Tooltip("Whether random animations are active.")]
    [SerializeField] private bool _randomAnimationsEnabled = true;

    // ── Animator trigger name constants ───────────────────────────────────────

    private const string T_BREATHING = "breathing";
    private const string T_TALKING   = "talking";
    private const string T_SHAKE_NO  = "shake_no";
    private const string T_BOB       = "bob";
    private const string T_CROAK     = "croak";
    private const string T_EYE_BULGE = "eye_bulge";
    private const string T_HUH       = "huh";
    private const string T_MELT      = "melt";
    private const string T_WAVING    = "waving";
    private const string T_NOD       = "nod";

    private static readonly string[] RandomTriggers = {
        T_BOB, T_CROAK, T_NOD, T_SHAKE_NO, T_WAVING, T_MELT, T_TALKING, T_EYE_BULGE
    };

    // ── Internals ─────────────────────────────────────────────────────────────

    private Animator _animator;
    private TextField _dialogueField;

    private bool _isTalking = false;
    private bool _introComplete = false;
    private bool _randomRunning = false;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        StartCoroutine(InitUI());
        StartCoroutine(PlayIntroSequence());
    }

    private void OnDisable()
    {
        UnregisterDialogueField();
    }

    // ── UI setup ──────────────────────────────────────────────────────────────

    private IEnumerator InitUI()
    {
        yield return null; // wait for UIDocument to build

        if (_uiDocument == null)
        {
            Debug.LogWarning("[CharacterAnimatorController] No UIDocument assigned — talking trigger disabled.");
            yield break;
        }

        var root = _uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[CharacterAnimatorController] rootVisualElement is null.");
            yield break;
        }

        // Log every TextField in the tree so we can confirm the name
        var allFields = root.Query<TextField>().ToList();
        Debug.Log($"[CharacterAnimatorController] Found {allFields.Count} TextField(s) in UIDocument:");
        foreach (var f in allFields)
            Debug.Log($"  name='{f.name}' class='{f.GetClasses()}'");

        _dialogueField = root.Q<TextField>(_dialogueFieldName);

        if (_dialogueField == null)
        {
            Debug.LogWarning($"[CharacterAnimatorController] Could not find TextField '{_dialogueFieldName}'.");
            yield break;
        }

        Debug.Log($"[CharacterAnimatorController] Watching TextField '{_dialogueFieldName}' for focus.");
        _dialogueField.RegisterCallback<FocusInEvent>(OnDialogueFocused);
        _dialogueField.RegisterCallback<FocusOutEvent>(OnDialogueUnfocused);
    }

    private void UnregisterDialogueField()
    {
        if (_dialogueField == null) return;
        _dialogueField.UnregisterCallback<FocusInEvent>(OnDialogueFocused);
        _dialogueField.UnregisterCallback<FocusOutEvent>(OnDialogueUnfocused);
    }

    // ── Intro sequence ────────────────────────────────────────────────────────

    private IEnumerator PlayIntroSequence()
    {
        Debug.Log("[CharacterAnimatorController] Intro sequence started.");
        yield return new WaitForSeconds(_introDelay);

        Debug.Log("[CharacterAnimatorController] Firing intro: melt.");
        FireTrigger(T_MELT);

        yield return new WaitForSeconds(_meltToWaveDelay);

        Debug.Log("[CharacterAnimatorController] Firing intro: waving.");
        FireTrigger(T_WAVING);

        // Wait for wave to likely finish before enabling randoms
        // Adjust this if your wave animation is longer/shorter
        yield return new WaitForSeconds(2f);

        _introComplete = true;

        if (_randomAnimationsEnabled)
            StartCoroutine(RandomAnimationLoop());
    }

    // ── Random animation loop ─────────────────────────────────────────────────

    private IEnumerator RandomAnimationLoop()
    {
        if (_randomRunning) yield break;
        _randomRunning = true;

        while (_randomAnimationsEnabled)
        {
            float wait = Random.Range(_randomIntervalMin, _randomIntervalMax);
            yield return new WaitForSeconds(wait);

            // Don't interrupt talking
            if (_isTalking) continue;

            string trigger = RandomTriggers[Random.Range(0, RandomTriggers.Length)];
            FireTrigger(trigger);
        }

        _randomRunning = false;
    }

    // ── Public methods (wire to ClickableObject events in Inspector) ──────────

    /// <summary>Wire to ClickableObject.OnObjectClicked</summary>
    public void OnCharacterClicked()
    {
        Debug.Log($"[CharacterAnimatorController] OnCharacterClicked called. isTalking={_isTalking}");
        if (_isTalking) return;
        FireTrigger(T_EYE_BULGE);
    }

    /// <summary>Wire to ClickableObject.OnObjectHeld</summary>
    public void OnCharacterHeld()
    {
        Debug.Log($"[CharacterAnimatorController] OnCharacterHeld called. isTalking={_isTalking}");
        if (_isTalking) return;
        FireTrigger(T_HUH);
    }

    // ── Dialogue focus callbacks ───────────────────────────────────────────────

    private void OnDialogueFocused(FocusInEvent evt)
    {
        if (_isTalking) return;
        _isTalking = true;
        FireTrigger(T_TALKING);
    }

    private void OnDialogueUnfocused(FocusOutEvent evt)
    {
        _isTalking = false;
        FireTrigger(T_BREATHING);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FireTrigger(string triggerName)
    {
        _animator.SetTrigger(triggerName);
        Debug.Log($"[CharacterAnimatorController] Trigger fired: {triggerName}");
    }
}