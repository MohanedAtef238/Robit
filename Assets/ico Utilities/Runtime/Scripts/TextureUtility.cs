using UnityEngine;

namespace Doji.Ico {

    internal static class TextureUtility {

        /// <summary>
        /// Creates a copy of <paramref name="texture"/> with the given
        /// <paramref name="resolution"/>.
        /// </summary>
        internal static Texture2D CopyResize(Texture2D texture, Vector2Int resolution) {
            return CopyResize(texture, resolution.x, resolution.y);
        }

        /// <summary>
        /// Creates a copy of <paramref name="texture"/> with the given
        /// <paramref name="targetWidth"/> and <paramref name="targetHeight"/>.
        /// </summary>
        internal static Texture2D CopyResize(Texture2D texture, int targetWidth, int targetHeight) {
            Texture2D result = new Texture2D(targetWidth, targetHeight);
            RenderTexture tmp = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture.active = tmp;
            Graphics.Blit(texture, tmp);
            result.filterMode = FilterMode.Bilinear;
            result.ReadPixels(new Rect(Vector2.zero, new Vector2(targetWidth, targetHeight)), 0, 0);
            result.Apply();
            RenderTexture.ReleaseTemporary(tmp);
            return result;
        }
    }
}