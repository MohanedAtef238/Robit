using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Coffee.UIEffects;

/// <summary>
/// Attached to each profile card (and the Plus card) in the Starting Page.
/// Call Setup() once after Instantiate to populate it.
/// All visual effects (edge shiny, shadow, glow) are handled by the UIEffect plugin.
/// This script only drives the scale-grow on hover and triggers the UIEffectTweener.
/// </summary>
public class ProfileCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    public Transform contentContainer;
    public Image profileImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statusText;

    [Header("Calibration UI")]
    public Button recalibrateButton;
    public Button deleteButton;

    [Header("Hover Scale")]
    public float hoverScale = 1.08f;
    public float hoverSpeed = 8f;

    private Action _onClick;
    private Action _onRecalibrate;
    private Action _onDelete;
    private Vector3 _baseScale;
    private Vector3 _contentBaseScale;
    private UIEffect _uiEffect;
    private UIEffectTweener _tweener;
    private Coroutine _scaleCoroutine;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (contentContainer != null) _contentBaseScale = contentContainer.localScale;
        
        _uiEffect = GetComponentInChildren<UIEffect>();
        _tweener = GetComponentInChildren<UIEffectTweener>();

        if (_uiEffect != null)
        {
            _uiEffect.edgeShinyAutoPlaySpeed = 0f;
            _uiEffect.edgeShinyRate = 0f;
        }

        if (recalibrateButton != null)
        {
            recalibrateButton.onClick.AddListener(() => _onRecalibrate?.Invoke());
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(() => _onDelete?.Invoke());
        }
    }

    // ── Setup ───────────────────────────────────────────────────────────────

    public void Setup(UserProfile profile, Sprite avatar, Action onClick, Action onRecalibrate, Action onDelete)
    {
        if (profileImage != null) profileImage.sprite = avatar;
        if (nameText    != null) nameText.text  = profile.name;
        
        if (statusText  != null) 
        {
            if (profile.isCalibrated) {
                statusText.text = string.IsNullOrEmpty(profile.lastCalibrationDate) || profile.lastCalibrationDate == "Never" 
                                    ? "Calibrated" 
                                    : "Calibrated : " + profile.lastCalibrationDate;
            } else {
                statusText.text = "Setup Required";
            }
        }

        if (recalibrateButton != null)
        {
            var txt = recalibrateButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = profile.isCalibrated ? "Recalibrate" : "Setup Now";
        }

        _onClick = onClick;
        _onRecalibrate = onRecalibrate;
        _onDelete = onDelete;
    }

    /// <summary>Populate the Plus card.</summary>
    public void SetupAsPlus(Action onClick)
    {
        if (nameText   != null) nameText.text = "Add Profile";
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (recalibrateButton != null) recalibrateButton.gameObject.SetActive(false);
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        _onClick = onClick;
    }

    // ── Pointer events ──────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        
        Vector3 target = _baseScale * hoverScale;
        _scaleCoroutine = StartCoroutine(ScaleTo(target));
        
        if (_uiEffect != null) _uiEffect.edgeShinyAutoPlaySpeed = 2f;
        if (_tweener != null) _tweener.PlayForward(true);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        
        Vector3 target = _baseScale;
        _scaleCoroutine = StartCoroutine(ScaleTo(target));
        
        if (_uiEffect != null) 
        {
            _uiEffect.edgeShinyAutoPlaySpeed = 0f;
            _uiEffect.edgeShinyRate = 0f;
        }
    }

    public void OnPointerClick(PointerEventData _) => _onClick?.Invoke();

    // ── Helpers ─────────────────────────────────────────────────────────────

    private IEnumerator ScaleTo(Vector3 target)
    {
        Transform targetTransform = transform;
        while (Vector3.Distance(targetTransform.localScale, target) > 0.001f)
        {
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, target, Time.deltaTime * hoverSpeed);
            yield return null;
        }
        targetTransform.localScale = target;
    }

    public void AnimatePopIn(float delay)
    {
        transform.localScale = Vector3.zero;
        StartCoroutine(PopInRoutine(delay));
    }

    private IEnumerator PopInRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        float elapsed  = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / duration;
            float scale = Mathf.Clamp01(t);
            // Elastic overshoot easing
            scale = Mathf.Sin(-13f * (scale + 1f) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * scale) + 1f;
            transform.localScale = _baseScale * scale;
            yield return null;
        }

        transform.localScale = _baseScale;
    }
}
