using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class CharacterAnimationController : MonoBehaviour
{
    [System.Serializable]
    public struct NamedAnimationClip
    {
        public string animationName;
        public AnimationClip clip;
        [Tooltip("Optional: Drop a sound effect here. Leave empty if this animation is silent.")]
        public AudioClip soundEffect;
    }

    [Header("Animation & Sound Library")]
    [SerializeField] private List<NamedAnimationClip> animationClips = new List<NamedAnimationClip>();

    private Animator animator;
    private AudioSource audioSource;
    private PlayableGraph graph;
    private AnimationClipPlayable currentPlayable;

    private Dictionary<string, NamedAnimationClip> clipDictionary;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 1.0 = Fully 3D sound. Change to 0 if you want 2D global sound!

        clipDictionary = new Dictionary<string, NamedAnimationClip>();
        foreach (var namedClip in animationClips)
        {
            if (namedClip.clip != null && !string.IsNullOrEmpty(namedClip.animationName))
            {
                clipDictionary[namedClip.animationName] = namedClip;
            }
        }
    }

    /// <summary>
    /// Triggers an animation clip by name and configures matching looping audio behavior.
    /// </summary>
    public void PlayAnimation(string animName)
    {
        if (clipDictionary == null) return;

        if (!clipDictionary.TryGetValue(animName, out NamedAnimationClip targetData))
        {
            Debug.LogWarning($"[Controller] Mismatch! The name '{animName}' was not found in the library.");
            return;
        }

        if (animator == null) return;

        // --- 1. STOP PREVIOUS AUDIO SAMPLES ---
        audioSource.Stop();

        // --- 2. HANDLE THE NEW SOUND EFFECT & LOOPING STATE ---
        if (targetData.soundEffect != null)
        {
            audioSource.clip = targetData.soundEffect;

            // Matches audio looping directly to your Animation Clip file settings!
            audioSource.loop = targetData.clip.isLooping;

            Debug.Log($"[Controller] Playing audio clip: {targetData.soundEffect.name} (Looping: {audioSource.loop})");
            audioSource.Play();
        }
        else
        {
            audioSource.clip = null;
            audioSource.loop = false;
            Debug.Log($"[Controller] Playing silent animation: {targetData.animationName}");
        }

        // --- 3. PLAY THE ANIMATION GRAPH ---
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        graph = PlayableGraph.Create($"{animName}_Graph");
        var output = AnimationPlayableOutput.Create(graph, "AnimationOutput", animator);
        currentPlayable = AnimationClipPlayable.Create(graph, targetData.clip);
        output.SetSourcePlayable(currentPlayable);

        graph.Play();
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    public void ResetToDefaultPose()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
        }

        if (animator != null)
        {
            animator.Rebind();
        }
    }
}