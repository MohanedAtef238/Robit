using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Coffee.UIEffects;
/// <summary>
/// Attached to each profile card (and the Plus card) in the Starting Page.
/// Call Setup() once after Instantiate to populate it.
/// </summary>
public class ProfileCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("3D Parallax Settings")]
    public float maxTiltAngle = 15f;
    public float tiltSpeed = 10f;
    private bool _isHovered = false;
    private RectTransform _rectTransform;
    private Vector3 _targetRotation;
    [Header("References")]
    public Image profileImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statusText;   // null on the Plus card — that's fine
    public Image borderImage;            // outline ring that lights up on hover

    [Header("Hover Settings")]
    public float hoverScale = 1.08f;
    public float hoverSpeed = 8f;

    private Action _onClick;
    private Vector3 _baseScale;
    private UIEffectTweener _tweener;

    void Awake()
    {
        _baseScale = transform.localScale;
        _rectTransform = GetComponent<RectTransform>();
        _tweener = GetComponent<UIEffectTweener>();
    }

    void Update()
    {
        if (_isHovered)
        {
            // Calculate mouse position relative to the center of the card
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, mousePos, null, out Vector2 localPoint);
            
            // Normalize the point based on the card's size
            float xPct = Mathf.Clamp(localPoint.x / (_rectTransform.rect.width / 2f), -1f, 1f);
            float yPct = Mathf.Clamp(localPoint.y / (_rectTransform.rect.height / 2f), -1f, 1f);

            // Tilt: Mouse up = tilt back (negative X), Mouse right = tilt right (negative Y)
            _targetRotation = new Vector3(-yPct * maxTiltAngle, xPct * maxTiltAngle, 0f);
        }
        else
        {
            _targetRotation = Vector3.zero;
        }

        // Smoothly interpolate current rotation to the target rotation
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(_targetRotation), Time.deltaTime * tiltSpeed);
    }

    /// <summary>Populate a normal profile card.</summary>
    public void Setup(UserProfile profile, Sprite avatar, Action onClick)
    {
        if (profileImage != null)  profileImage.sprite = avatar;
        if (nameText != null)      nameText.text = profile.name;
        if (statusText != null)    statusText.text = profile.isCalibrated ? "Calibrated" : "Setup Required";
        if (borderImage != null)   borderImage.color = Color.clear;
        _onClick = onClick;
    }

    /// <summary>Populate the Plus card.</summary>
    public void SetupAsPlus(Action onClick)
    {
        if (nameText != null)    nameText.text = "Add Profile";
        if (statusText != null)  statusText.gameObject.SetActive(false);
        if (borderImage != null) borderImage.color = Color.clear;
        _onClick = onClick;
    }

    // ── Pointer events ─────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        _isHovered = true;
        StopAllCoroutines();
        StartCoroutine(ScaleTo(_baseScale * hoverScale));
        if (borderImage != null) borderImage.color = Color.white;
        if (_tweener != null) _tweener.PlayForward(true);
    }

    public void OnPointerExit(PointerEventData _)
    {
        _isHovered = false;
        StopAllCoroutines();
        StartCoroutine(ScaleTo(_baseScale));
        if (borderImage != null) borderImage.color = Color.clear;
    }

    public void OnPointerClick(PointerEventData _) => _onClick?.Invoke();

    private System.Collections.IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * hoverSpeed);
            yield return null;
        }
        transform.localScale = target;
    }

    public void AnimatePopIn(float delay)
    {
        transform.localScale = Vector3.zero;
        StartCoroutine(PopInRoutine(delay));
    }

    private System.Collections.IEnumerator PopInRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        float elapsed = 0f;
        float duration = 0.4f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Aggressive elastic pop easing (overshoot)
            float scale = Mathf.Clamp01(t);
            scale = Mathf.Sin(-13f * (scale + 1f) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * scale) + 1f;
            
            transform.localScale = _baseScale * scale;
            yield return null;
        }
        transform.localScale = _baseScale;
    }
}
