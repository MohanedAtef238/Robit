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
    private List<Vector3> initialPositions = new List<Vector3>(); // Added to keep lids attached

    void Start()
    {
        foreach (var lid in eyelids)
        {
            if (lid.eyelidTransform != null)
            {
                initialRotations.Add(lid.eyelidTransform.localRotation);
                initialPositions.Add(lid.eyelidTransform.localPosition); // Store local anchor
            }
        }
        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            float staggeredDelay = 1f / blinkSpeed;

            for (int i = 0; i < eyelids.Count; i++)
            {
                if (eyelids[i].eyelidTransform != null)
                {
                    float finalDelay = eyelids[i].blinkOrder * staggeredDelay;
                    // Pass the initial position into the blink
                    StartCoroutine(PerformBlink(eyelids[i], initialRotations[i], initialPositions[i], finalDelay));
                }
            }
        }
    }

    IEnumerator PerformBlink(EyelidData lid, Quaternion openRotation, Vector3 localAnchor, float delay)
    {
        yield return new WaitForSeconds(delay);

        Quaternion closedRotation = openRotation * Quaternion.Euler(0, 0, blinkRotation * lid.directionMultiplier);

        // Snap Shut
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            lid.eyelidTransform.localRotation = Quaternion.Lerp(openRotation, closedRotation, t);

            // Re-enforce local position every frame to prevent "drifting" during bobs
            lid.eyelidTransform.localPosition = localAnchor;
            yield return null;
        }

        // Snap Open
        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            lid.eyelidTransform.localRotation = Quaternion.Lerp(closedRotation, openRotation, t);

            // Re-enforce local position
            lid.eyelidTransform.localPosition = localAnchor;
            yield return null;
        }

        // Final safety snap
        lid.eyelidTransform.localPosition = localAnchor;
        lid.eyelidTransform.localRotation = openRotation;
    }


}
