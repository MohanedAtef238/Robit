using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;

public class CharacterAnimationManager : MonoBehaviour
{
    private Animator _animator;

    [Header("UI Document Connections")]
    [SerializeField] private UIDocument macrosDocument;
    [SerializeField] private UIDocument settingsDocument;

    [Header("Click Settings")]
    [SerializeField] private GameObject clickableObject;

    [Header("Hold Settings")]
    [SerializeField] private float holdDurationThreshold = 1.5f;

    [Header("Random Idle Settings")]
    [SerializeField] private float minRandomInterval = 4f;
    [SerializeField] private float maxRandomInterval = 8f;

    // State Lock: Prevents random background triggers from interrupting specific rules
    private bool _isActionLocked = false;

    // Track input interactions
    private float _mouseHoldTimer = 0f;
    private bool _isHoldingMouse = false;
    private bool _holdActionTriggered = false;

    // Co-routine references
    private Coroutine _talkingRoutine;
    private Coroutine _randomRoutine;

    private readonly string[] _randomPool = { "breathing", "shake_no", "bob", "croak", "huh", "nod" };

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        // Rule 1: Fixed Startup Sequence
        StartCoroutine(StartSequenceRoutine());

        // Setup UI hooks
        SetupMacrosDocument();
        SetupInputButton();

        // Background Rule: Start the random background idles loop
        _randomRoutine = StartCoroutine(RandomBackgroundRoutine());
    }

    void Update()
    {
        HandleClickAndHoldInputs();
    }

    // ====================================================================
    // RULE 1: FIXING STARTUP SEQUENCE (Melt -> Wave)
    // ====================================================================
    private IEnumerator StartSequenceRoutine()
    {
        _isActionLocked = true; // Lock out the randomizer

        // Let the animator spin up fully on frame 1
        yield return new WaitForSeconds(0.1f);

        _animator.SetTrigger("melt");
        Debug.Log("[AnimManager] Core execution pass: melt");

        // FIXED GAP: You must wait the duration of the melt animation clip 
        // before firing 'waving', otherwise 'waving' cuts 'melt' off immediately.
        yield return new WaitForSeconds(2.2f);

        _animator.SetTrigger("waving");
        Debug.Log("[AnimManager] Core execution pass: waving");

        // Wait for waving to finish before letting background animations run
        yield return new WaitForSeconds(2.0f);

        _isActionLocked = false; // Release lock
    }

    // ====================================================================
    // BACKGROUND RULE: RANDOM ANIMATIONS LOOP
    // ====================================================================
    private IEnumerator RandomBackgroundRoutine()
    {
        while (true)
        {
            // Calculate random interval delay first
            float delay = Random.Range(minRandomInterval, maxRandomInterval);
            yield return new WaitForSeconds(delay);

            // Only fire if an explicit priority rule isn't currently running
            if (!_isActionLocked && _animator != null)
            {
                string chosenTrigger = _randomPool[Random.Range(0, _randomPool.Length)];
                _animator.SetTrigger(chosenTrigger);
                Debug.Log($"[AnimManager] Background Randomizer fired: {chosenTrigger}");
            }
        }
    }

    // ====================================================================
    // RULES 2 & 3: CLICK (Eye Bulge) AND MOUSE HOLD (Huh)
    // ====================================================================
    private void HandleClickAndHoldInputs()
    {
        if (Mouse.current == null || clickableObject == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    GameObject hitObj = hit.collider.gameObject;
                    if (hitObj == clickableObject || hitObj.transform.IsChildOf(clickableObject.transform))
                    {
                        _isHoldingMouse = true;
                        _holdActionTriggered = false;
                        _mouseHoldTimer = 0f;
                    }
                }
            }
        }

        if (_isHoldingMouse && Mouse.current.leftButton.isPressed)
        {
            _mouseHoldTimer += Time.deltaTime;

            if (_mouseHoldTimer >= holdDurationThreshold && !_holdActionTriggered)
            {
                _isActionLocked = true; // Momentary lock
                _animator.SetTrigger("huh");
                Debug.Log("[AnimManager] Hold condition met. Fired: huh");
                _holdActionTriggered = true;
                StartCoroutine(ReleaseLockAfterDelay(1.5f));
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_isHoldingMouse && !_holdActionTriggered)
            {
                _isActionLocked = true; // Momentary lock
                _animator.SetTrigger("eye_bulge");
                Debug.Log("[AnimManager] Click condition met. Fired: eye_bulge");
                StartCoroutine(ReleaseLockAfterDelay(1.5f));
            }

            _isHoldingMouse = false;
            _holdActionTriggered = false;
            _mouseHoldTimer = 0f;
        }
    }

    // ====================================================================
    // RULE 4: MACROS DOCUMENT (Any button clicked -> Nod)
    // ====================================================================
    private void SetupMacrosDocument()
    {
        if (macrosDocument == null) return;
        VisualElement root = macrosDocument.rootVisualElement;
        if (root == null) return;

        root.Query<Button>().ForEach(btn =>
        {
            btn.RegisterCallback<ClickEvent>(evt =>
            {
                _isActionLocked = true;
                _animator.SetTrigger("nod");
                Debug.Log($"[AnimManager] Macros UI intercept. Fired: nod");
                StartCoroutine(ReleaseLockAfterDelay(1.2f));
            });
        });
    }

    // ====================================================================
    // RULE 5: INPUT BUTTON LINK (Repeat Talking)
    // ====================================================================
    private void SetupInputButton()
    {
        if (settingsDocument == null) return;
        VisualElement root = settingsDocument.rootVisualElement;
        if (root == null) return;

        Button inputBtn = root.Q<Button>("inputButton");
        if (inputBtn != null)
        {
            inputBtn.RegisterCallback<ClickEvent>(evt =>
            {
                if (_talkingRoutine != null) StopCoroutine(_talkingRoutine);
                _talkingRoutine = StartCoroutine(RepeatTalkingRoutine());
            });
        }
    }

    private IEnumerator RepeatTalkingRoutine()
    {
        _isActionLocked = true; // Lock out random actions for the duration of talking

        for (int i = 0; i < 4; i++)
        {
            _animator.SetTrigger("talking");
            Debug.Log($"[AnimManager] Looping execution: talking ({i + 1}/4)");
            yield return new WaitForSeconds(1.4f);
        }

        _talkingRoutine = null;
        _isActionLocked = false; // Return handling to background randomizer
    }

    // Helper safety logic to reset lock states
    private IEnumerator ReleaseLockAfterDelay(float time)
    {
        yield return new WaitForSeconds(time);
        _isActionLocked = false;
    }
}