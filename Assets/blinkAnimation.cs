using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EyeBlinker : MonoBehaviour
{
    [System.Serializable]
    public class EyelidData
    {
        public string name;
        public Transform eyelidTransform;
        [Tooltip("Use 1 for top lids, -1 for bottom lids.")]
        public float directionMultiplier = 1f;
        [Tooltip("0 for first eye, 1 for second eye to trigger mid-blink.")]
        public int blinkOrder = 0;
    }

    [Header("Eyelid Assignment")]
    public List<EyelidData> eyelids = new List<EyelidData>();

    [Header("Blink Settings")]
    public float blinkRotation = 60f;
    public float blinkSpeed = 10f;
    public Vector2 blinkIntervalRange = new Vector2(2f, 5f);

    private List<Quaternion> initialRotations = new List<Quaternion>();

    void Start()
    {
        foreach (var lid in eyelids)
        {
            if (lid.eyelidTransform != null)
                initialRotations.Add(lid.eyelidTransform.localRotation);
        }
        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            // Calculate the delay for the "midway" effect.
            // Since Lerp takes 1/blinkSpeed seconds to close, 
            // the second eye starts right as the first one finishes closing.
            float staggeredDelay = 1f / blinkSpeed;

            for (int i = 0; i < eyelids.Count; i++)
            {
                if (eyelids[i].eyelidTransform != null)
                {
                    // Delay is based on the blinkOrder (0 * delay = instant, 1 * delay = midway)
                    float finalDelay = eyelids[i].blinkOrder * staggeredDelay;
                    StartCoroutine(PerformBlink(eyelids[i], initialRotations[i], finalDelay));
                }
            }
        }
    }

    IEnumerator PerformBlink(EyelidData lid, Quaternion openRotation, float delay)
    {
        yield return new WaitForSeconds(delay);

        Quaternion closedRotation = openRotation * Quaternion.Euler(0, 0, blinkRotation * lid.directionMultiplier);

        // Snap Shut
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            lid.eyelidTransform.localRotation = Quaternion.Lerp(openRotation, closedRotation, t);
            yield return null;
        }

        // Snap Open
        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            lid.eyelidTransform.localRotation = Quaternion.Lerp(closedRotation, openRotation, t);
            yield return null;
        }
    }
}