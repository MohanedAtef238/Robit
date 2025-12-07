using UnityEngine;

public class OverlayManager : MonoBehaviour
{
    void Start()
    {
        #if !UNITY_EDITOR
        WindowManager.SetOverlaySize();
        #endif
    }
}
