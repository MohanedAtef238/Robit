using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Applies a full-scene background blur while the Home dashboard is open.
/// Creates a runtime global Volume and animates its weight in/out.
public class HomeBackgroundBlurController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool autoFindCamera = true;
    [SerializeField] private float transitionDuration = 0.22f;
    [SerializeField, Range(0f, 1f)] private float activeWeight = 1f;

    [Header("Gaussian DOF")]
    [SerializeField] private float gaussianStart = 0.2f;
    [SerializeField] private float gaussianEnd = 2.5f;
    [SerializeField] private float gaussianRadius = 1f;
    [SerializeField] private bool highQualitySampling;

    private Volume _volume;
    private VolumeProfile _runtimeProfile;
    private UniversalAdditionalCameraData _cameraData;
    private Coroutine _transitionRoutine;
    private bool _initialized;

    public void Initialize()
    {
        if (_initialized) return;

        ResolveCamera();
        EnsurePostProcessingEnabled();
        EnsureVolumeAndProfile();

        _volume.weight = 0f;
        _initialized = true;
    }

    public void SetBlurActive(bool active)
    {
        Initialize();

        if (_volume == null)
            return;

        float target = active ? activeWeight : 0f;
        if (transitionDuration <= 0f)
        {
            _volume.weight = target;
            return;
        }

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(AnimateWeight(target));
    }

    private IEnumerator AnimateWeight(float target)
    {
        float start = _volume.weight;
        float t = 0f;

        while (t < transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / transitionDuration);
            _volume.weight = Mathf.Lerp(start, target, k);
            yield return null;
        }

        _volume.weight = target;
        _transitionRoutine = null;
    }

    private void ResolveCamera()
    {
        if (targetCamera != null)
            return;

        if (!autoFindCamera)
            return;

        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindFirstObjectByType<Camera>();
    }

    private void EnsurePostProcessingEnabled()
    {
        if (targetCamera == null)
        {
            RobitLogger.LogWarning("[HomeBackgroundBlur] No camera found; blur volume can still exist but may not render.");
            return;
        }

        if (!targetCamera.TryGetComponent(out _cameraData))
            _cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        _cameraData.renderPostProcessing = true;
    }

    private void EnsureVolumeAndProfile()
    {
        _volume = GetComponent<Volume>();
        if (_volume == null)
            _volume = gameObject.AddComponent<Volume>();

        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeProfile.name = "HomeRuntimeBlurProfile";

        var dof = _runtimeProfile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(gaussianStart);
        dof.gaussianEnd.Override(gaussianEnd);
        dof.gaussianMaxRadius.Override(gaussianRadius);
        dof.highQualitySampling.Override(highQualitySampling);
        dof.active = true;

        _volume.isGlobal = true;
        _volume.priority = 100f;
        _volume.profile = _runtimeProfile;
    }

    private void OnDestroy()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        if (_runtimeProfile != null)
            Destroy(_runtimeProfile);
    }
}
