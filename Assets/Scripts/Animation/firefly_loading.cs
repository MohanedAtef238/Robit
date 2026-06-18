using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FireflyLoadingScreen : MonoBehaviour
{
    [Header("UI Canvas Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image overlayImage;

    [Header("3D Firefly References")]
    [Tooltip("The parent 3D firefly object that will fly around the screen.")]
    [SerializeField] private Transform fireflyTarget;
    [Tooltip("The left wing transform that will rotate.")]
    [SerializeField] private Transform leftWing;
    [Tooltip("The right wing transform that will rotate.")]
    [SerializeField] private Transform rightWing;

    [Header("Flight Settings")]
    [SerializeField] private float flightSpeed = 2f;
    [SerializeField] private float flightRadiusX = 300f;
    [SerializeField] private float flightRadiusY = 150f;

    [Header("Wing Flap Settings")]
    [SerializeField] private float flapSpeed = 15f;
    [Tooltip("The maximum angle variation for the wing flap.")]
    [SerializeField] private float maxFlapAngle = 35f;

    [Header("Screen Configuration")]
    [SerializeField] private bool isLoading = true;
    [SerializeField] private float fadeSpeed = 4f;

    private CanvasGroup m_CanvasGroup;
    private Vector3 fireflyStartPosition;
    private float flightTimer;

    private void Awake()
    {
        // Cache or add the CanvasGroup for smooth screen fading
        m_CanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        m_CanvasGroup.alpha = isLoading ? 1f : 0f;

        if (fireflyTarget != null)
        {
            fireflyStartPosition = fireflyTarget.localPosition;
        }
    }

    private void Update()
    {
        // Handle screen fading in and out based on state
        float targetAlpha = isLoading ? 1f : 0f;
        if (!Mathf.Approximately(m_CanvasGroup.alpha, targetAlpha))
        {
            m_CanvasGroup.alpha = Mathf.Lerp(m_CanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        // Only animate elements if the loading screen is fully or partially visible
        if (m_CanvasGroup.alpha > 0)
        {
            AnimateFirefly();
        }
    }

    private void AnimateFirefly()
    {
        flightTimer += Time.deltaTime * flightSpeed;

        // 1. Flight Movement: Creates a smooth infinity/Lissajous loop pattern across the screen
        if (fireflyTarget != null)
        {
            float xOffset = Mathf.Sin(flightTimer) * flightRadiusX;
            float yOffset = Mathf.Sin(flightTimer * 2f) * flightRadiusY; // Fast Y creates an '8' loop

            fireflyTarget.localPosition = fireflyStartPosition + new Vector3(xOffset, yOffset, 0f);
        }

        // 2. Wing Flapping: Uses a rapid sine wave to pivot wings back and forth
        if (leftWing != null && rightWing != null)
        {
            float flapAngle = Mathf.Sin(Time.time * flapSpeed) * maxFlapAngle;

            // Rotate wings in opposite directions along their local Y axis to match biological symmetry
            leftWing.localRotation = Quaternion.Euler(0f, flapAngle, 0f);
            rightWing.localRotation = Quaternion.Euler(0f, -flapAngle, 0f);
        }
    }

    // Public property so other level loading scripts can toggle this screen cleanly
    public bool IsLoading
    {
        get => isLoading;
        set => isLoading = value;
    }
}