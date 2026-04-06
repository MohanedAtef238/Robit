using System;
using UnityEngine;

namespace Doji.Ico {

    public enum DownscaleMode {
        /// <summary>
        /// Do not not rescale the texture.
        /// (Unless the resolution exceeds the maximum supported dimensions 256x256).
        /// </summary>
        None,

        /// <summary>
        /// Rescale the longest side of the texture to the nearest power of 2 (or 256 at most).
        /// </summary>
        ToNearest,

        /// <summary>
        /// Rescale the longest side of the texture to the next larger power of 2 (or 256 at most).
        /// </summary>
        ToLarger,

        /// <summary>
        /// Rescale the longest side of the texture to the next smaller power of 2 (or 256 at most).
        /// </summary>
        ToSmaller
    }

    public class IcoRescaleSettings {

        [SerializeField]
        private int _multiResolution = 1;

        [SerializeField]
        private int _downscaleMode = 1;

        /// <summary>
        /// If set to true, multiple resolutions for the icon are automatically
        /// created by continually halving the initial resolution.
        /// The initial resolution is chosen based on <see cref="DownscaleMode"/>.
        /// The image is downscaled until the shortest side is smaller than MIN_SIZE.
        /// Setting this to false means, the single source texture is being used as is,
        /// which means you have to make sure the source texture is within the supported
        /// dimensions (256x256).
        /// </summary>
        public bool MultiResolutionIcon {
            get {
                return _multiResolution != 0;
            }
            set {
                _multiResolution = (value ? 1 : 0);
            }
        }

        /// <summary>
        /// The scaling mode.
        /// Only has an effect, if <see cref="MultiResolutionIcon"/> is set to true.
        /// </summary>
        public DownscaleMode DownscaleMode {
            get {
                return (DownscaleMode)_downscaleMode;
            }
            set {
                _downscaleMode = (int)value;
            }
        }

        private const int MIN_SIZE = 16;

        internal Vector2Int GetTargetResolution(int width, int height) {
            float ratio;

            if (MultiResolutionIcon) {
                switch (DownscaleMode) {
                    case DownscaleMode.None:
                        int longestEdge = Mathf.Max(width, height);
                        int targetSize = Mathf.Min(longestEdge, Icon.Image.MAX_SIZE);
                        ratio = (float)targetSize / longestEdge;
                        break;
                    case DownscaleMode.ToNearest:
                        longestEdge = Mathf.Max(width, height);
                        targetSize = Mathf.Min(ToNearest(longestEdge), Icon.Image.MAX_SIZE);
                        ratio = (float)targetSize / longestEdge;
                        break;
                    case DownscaleMode.ToLarger:
                        longestEdge = Mathf.Max(width, height);
                        targetSize = Mathf.Min(ToLarger(longestEdge), Icon.Image.MAX_SIZE);
                        ratio = (float)targetSize / longestEdge;
                        break;
                    case DownscaleMode.ToSmaller:
                        longestEdge = Mathf.Max(width, height);
                        targetSize = Mathf.Min(ToSmaller(longestEdge), Icon.Image.MAX_SIZE);
                        ratio = (float)targetSize / longestEdge;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown downscale mode: " + DownscaleMode);
                }
            } else {
                ratio = 1f;
            }

            Vector2Int maxRes = new Vector2Int((int)(width * ratio), (int)(height * ratio));
            if (maxRes.x > Icon.Image.MAX_SIZE || maxRes.y > Icon.Image.MAX_SIZE) {
                throw new ArgumentException($"Invalid texture resolution. ICO files support a maximum resolution of {Icon.Image.MAX_SIZE}x{Icon.Image.MAX_SIZE}.");
            }

            return maxRes;
        }

        private static int ToNearest(int x) {
            int next = ToLarger(x);
            int prev = next >> 1;
            return next - x < x - prev ? next : prev;
        }

        private static int ToLarger(int x) {
            if (x < 0) { return 0; }
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        private static int ToSmaller(int x) {
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x ^ (x >> 1);
        }

        /// <summary>
        /// Returns the number of resolutions that need to be stored if the largest image
        /// has a longest side of <paramref name="n"/> pixels, if each resolution has half
        /// the amount of pixels than the previous one.
        /// </summary>
        internal int GetNumImages(int width, int height) {
            Vector2Int maxRes = GetTargetResolution(width, height);
            return MultiResolutionIcon ? Quot(Mathf.Min(maxRes.x, maxRes.y)) + 1 : 1;
        }

        /// <summary>
        /// Returns the number of times <paramref name="n"/> can be divided by 2 until it's smaller than MIN_SIZE
        /// </summary>
        private static int Quot(int n) {
            return (int)Math.Floor(Math.Log(n / MIN_SIZE, 2));
        }
    }
}