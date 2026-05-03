using UnityEngine;

namespace Doji.Ico {

    public static class IconUnityExtensions {

        /// <summary>
        /// Extracts the given image from this icon as a Texture2D
        /// </summary>
        /// <param name="icon">The icon to extract the image from.</param>
        /// <param name="imageIndex">The index of the image to extract.</param>
        /// <param name="mipChain">Whether to create the texture with mipmaps or without.</param>
        /// <param name="markNonReadable">Optionally marks the texture as non-readable.</param>
        /// <returns>The extracted texture</returns>
        public static Texture2D ExtractTexture2D(this Icon icon, int imageIndex = 0, bool mipChain = true, bool markNonReadable = false) {
            return IcoConversion.Decode(icon, imageIndex, mipChain, markNonReadable);
        }
    }
}
