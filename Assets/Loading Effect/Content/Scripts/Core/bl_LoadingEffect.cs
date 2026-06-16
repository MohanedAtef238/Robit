using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class bl_LoadingEffect : MonoBehaviour
{

    [Header("Settings")]
    // 1. FIXED: Restored 'ID' as a public field so bl_LoadingUtils can read it
    public int ID;

    // 2. FIXED: Kept private 'Loading' with lowercase 'isLoading' property for protection level matching
    [SerializeField] private bool Loading = false;
    public float FadeSpeed = 4;

    [Header("Firefly Asset References")]
    [SerializeField] private Image BackgroundImage;
    [SerializeField] private Image OverlayImage;
    [Space(5)]
    [Tooltip("The parent 3D firefly object that will fly around.")]
    [SerializeField] private Transform FireflyTarget;
    [Tooltip("The left wing transform that will rotate.")]
    [SerializeField] private Transform LeftWing;
    [Tooltip("The right wing transform that will rotate.")]
    [SerializeField] private Transform RightWing;

    [Header("Firefly Flight Settings")]
    [SerializeField] private float FlightSpeed = 2f;
    [SerializeField] private float FlightRadiusX = 300f;
    [SerializeField] private float FlightRadiusY = 150f;

    [Header("Firefly Wing Flap Settings")]
    [SerializeField] private float FlapSpeed = 15f;
    [SerializeField] private float MaxFlapAngle = 35f;

    [Header("Legacy References")]
    [SerializeField] private LoadingUIInfo[] LoadingUI;

    [HideInInspector] public bool ShowList = true;
    private CanvasGroup m_CanvasGroup;

    private Vector3 fireflyStartPosition;
    private float flightTimer;

    void Awake()
    {
        if (GetComponent<CanvasGroup>() != null)
        {
            m_CanvasGroup = GetComponent<CanvasGroup>();
        }
        else
        {
            m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        m_CanvasGroup.alpha = (Loading) ? 1 : 0;

        if (FireflyTarget != null)
        {
            fireflyStartPosition = FireflyTarget.localPosition;
        }
    }

    void Start()
    {
        if (LoadingUI != null)
        {
            for (int i = 0; i < LoadingUI.Length; i++)
            {
                LoadingUI[i].Init();
            }
        }
    }

    void Update()
    {
        if (Loading)
        {
            OnLoading();
        }
        else
        {
            OnUnLoading();
        }
    }

    void OnLoading()
    {
        if (m_CanvasGroup.alpha > 0)
        {
            AnimateCustomFirefly();
        }

        if (LoadingUI != null && LoadingUI.Length > 0)
        {
            for (int i = 0; i < LoadingUI.Length; i++)
            {
                if (LoadingUI[i].UI != null)
                {
                    if (LoadingUI[i].NextDelay > Time.time)
                    {
                        continue;
                    }
                    if (LoadingUI[i].m_Type == LoadingEffectType.Rotate)
                    {
                        LoadingUI[i].UI.Rotate(((LoadingUI[i].Axis * (LoadingUI[i].Speed * 10)) * Time.deltaTime), Space.World);
                    }
                    else if (LoadingUI[i].m_Type == LoadingEffectType.Filled)
                    {
                        if (LoadingUI[i].PingPong)
                        {
                            if (LoadingUI[i].Forward)
                            {
                                LoadingUI[i].Value += Time.deltaTime * (LoadingUI[i].Speed / 10);
                                LoadingUI[i].Image.fillAmount = LoadingUI[i].Curve.Evaluate(LoadingUI[i].Value);
                                if (LoadingUI[i].Image.fillAmount >= 1) { LoadingUI[i].Forward = !LoadingUI[i].Forward; }
                            }
                            else
                            {
                                LoadingUI[i].Value -= Time.deltaTime * (LoadingUI[i].Speed / 10);
                                LoadingUI[i].Image.fillAmount = LoadingUI[i].Curve.Evaluate(LoadingUI[i].Value);
                                if (LoadingUI[i].Image.fillAmount <= 0) { LoadingUI[i].Forward = !LoadingUI[i].Forward; }
                            }
                        }
                    }
                }
            }
        }

        if (m_CanvasGroup.alpha < 1)
        {
            m_CanvasGroup.alpha = Mathf.Lerp(m_CanvasGroup.alpha, 1, Time.deltaTime * FadeSpeed);
        }
    }

    private void AnimateCustomFirefly()
    {
        flightTimer += Time.deltaTime * FlightSpeed;

        if (FireflyTarget != null)
        {
            float xOffset = Mathf.Sin(flightTimer) * FlightRadiusX;
            float yOffset = Mathf.Sin(flightTimer * 2f) * FlightRadiusY;
            FireflyTarget.localPosition = fireflyStartPosition + new Vector3(xOffset, yOffset, 0f);
        }

        if (LeftWing != null && RightWing != null)
        {
            float flapAngle = Mathf.Sin(Time.time * FlapSpeed) * MaxFlapAngle;
            LeftWing.localRotation = Quaternion.Euler(0f, flapAngle, 0f);
            RightWing.localRotation = Quaternion.Euler(0f, -flapAngle, 0f);
        }
    }

    void OnUnLoading()
    {
        if (m_CanvasGroup.alpha > 0)
        {
            m_CanvasGroup.alpha = Mathf.Lerp(m_CanvasGroup.alpha, 0, Time.deltaTime * FadeSpeed);
        }
    }

    // 3. FIXED: Ensured lowercase 'isLoading' matches exact spelling and public getter/setter rules
    public bool isLoading
    {
        get { return Loading; }
        set { Loading = value; }
    }
}