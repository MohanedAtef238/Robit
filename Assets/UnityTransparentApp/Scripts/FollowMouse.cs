/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FollowMouse : MonoBehaviour
{
    private void Update() {
        if (Camera.main != null && Mouse.current != null) {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            mousePos.z = -Camera.main.transform.position.z;
            transform.position = Camera.main.ScreenToWorldPoint(mousePos);
        }
    }
}
