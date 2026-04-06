using Doji.Ico;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveIconSample : MonoBehaviour {
    public RawImage Image;

    private void Awake() {
        GetComponentInChildren<Button>().onClick.AddListener(OnLoadImageClicked);
    }

    private void OnLoadImageClicked() {
#if UNITY_EDITOR
        string path = EditorUtility.SaveFilePanel("Save .uci", "", Image.texture.name, "ico");
        if (!string.IsNullOrEmpty(path)) {
            byte[] icoData = IcoConversion.EncodeToICO(Image.texture as Texture2D);
            File.WriteAllBytes(path, icoData);
        }
#else
        Debug.LogWarning("SaveIconSample is editor-only and cannot show a save file dialog at runtime.");
#endif
    }
}
