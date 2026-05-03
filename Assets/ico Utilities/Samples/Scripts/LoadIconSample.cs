using Doji.Ico;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This class shows how to extract a Texture2D from an .ico file
/// </summary>
public class LoadIconSample : MonoBehaviour {

    public Image[] UIImages;

    public string FilePath = @"Assets\ico Utilities\Samples\SampleIcons\microsoft.com.ico";

    private void Awake() {
        GetComponentInChildren<Button>().onClick.AddListener(OnLoadImageClicked);
    }

    private void OnLoadImageClicked() {
        byte[] data = File.ReadAllBytes(FilePath);
        Icon icon = IcoConversion.LoadIcon(data);
        for (int i = 0; i < icon.Images.Length; i++) {
            Texture2D texture = icon.ExtractTexture2D(i);
            string name = $"{Path.GetFileName(FilePath)}_{i}_{icon.Images[i].Width}x{icon.Images[i].Height}";
            texture.name = name;
            Sprite sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
            sprite.name = name;
            UIImages[i].gameObject.SetActive(true);
            UIImages[i].sprite = sprite;
        }
    }

    private void OnDestroy() {
        for (int i = 0; i < UIImages.Length; i++) {
            if (UIImages[i].sprite.texture != null) {
                DestroyImmediate(UIImages[i].sprite.texture);
            }
        }
    }
}

