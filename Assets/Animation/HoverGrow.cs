using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class UIStyleManager : MonoBehaviour
{
    // List only the names that aren't nested inside each other
    private readonly List<string> targetNames = new List<string>
    {
        "clockWidget", "reminder", "sliders", "dialogueBox",
        "mick", "keyboard", "exit", "refresh"
    };

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        foreach (string n in targetNames)
        {
            VisualElement target = root.Q(n);
            if (target != null)
            {
                // Assign the USS class we wrote in Step 1
                target.AddToClassList("hover-scaler");

                // Ensure the mouse can "see" the element
                target.pickingMode = PickingMode.Position;

                // Stop internal children from confusing the hover state
                target.Query<VisualElement>().ForEach(c => {
                    if (c != target) c.pickingMode = PickingMode.Ignore;
                });
            }
        }
    }
}