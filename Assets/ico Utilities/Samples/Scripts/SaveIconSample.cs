using Doji.Ico;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SaveIconSample : MonoBehaviour {
    public RawImage Image;

    private void Awake() {
        GetComponentInChildren<Button>().onClick.AddListener(OnLoadImageClicked);
    }

    private void OnLoadImageClicked() {
         string path = EditorUtility.SaveFilePanel("Save .uci", "", Image.texture.name, "ico");
        if (!string.IsNullOrEmpty(path)) {
            byte[] icoData = IcoConversion.EncodeToICO(Image.texture as Texture2D);
            File.WriteAllBytes(path, icoData);
        }
    }
}
