using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class Transparency : MonoBehaviour
{
    [SerializeField] private bool alwaysInteractive = true;

    void Start()
    {
        #if !UNITY_EDITOR
        WindowManager.GetWindowHandle();
        WindowManager.MakeTransparent();
        
        if (alwaysInteractive)
        {
            WindowManager.SetClickThrough(false);
        }
        Debug.Log("[Transparency] Window initialized");
        #endif
    }
}
