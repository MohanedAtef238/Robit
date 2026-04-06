using System;

namespace Doji.Ico {
    public class Icon {

        public struct Image {

            internal const int MAX_SIZE = 256;

            /// <summary>
            /// The image width in pixels.
            /// Can be any number between 0 and 255.
            /// Value 0 means image width is 256 pixels.
            /// </summary>
            public int Width {
                get {
                    return _width == 0 ? MAX_SIZE : _width;
                }
                internal set {
                    if (value < 0 || value > MAX_SIZE) {
                        throw new ArgumentException($"Width must be between 0 and {MAX_SIZE}");
                    }
                    _width = (byte)(value == MAX_SIZE ? 0 : value);
                }
            }
            internal byte _width;

            /// <summary>
            /// The image height in pixels.
            /// Can be any number between 0 and 255.
            /// Value 0 means image height is 256 pixels.
            /// </summary>
            public int Height {
                get {
                    return _height == 0 ? MAX_SIZE : _height;
                }
                internal set {
                    if (value < 0 || value > MAX_SIZE) {
                        throw new ArgumentException($"Height must be between 0 and {MAX_SIZE}");
                    }
                    _height = (byte)(value == MAX_SIZE ? 0 : value);
                }
            }
            internal byte _height;

            /// <summary>
            /// The number of colors in the color palette.
            /// 0 if the image does not use a color palette
            /// </summary>
            public byte NumColors { get; internal set; }

            /// <summary>
            /// In ICO format: Specifies color planes. Should be 0 or 1.[Notes 3]
            /// In CUR format: Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
            /// </summary>
            public short ColorPlanes { get; internal set; }

            /// <summary>
            /// In ICO format: Specifies bits per pixel. [Notes 4]
            /// In CUR format: Specifies the vertical coordinates of the hotspot in number of pixels from the top.
            /// </summary>
            public short BitsPerPixel { get; internal set; }

            /// <summary>
            /// The size of the image's data in bytes
            /// </summary>
            public int ImageSize { get; internal set; }

            /// <summary>
            /// The offset of BMP or PNG data
            /// from the beginning of the ICO/CUR file.
            /// </summary>
            public int Offset { get; internal set; }

            /// <summary>
            /// The image data.
            /// It may be in either Windows BMP format,
            /// excluding the BITMAPFILEHEADER structure,
            /// or in PNG format, stored in its entirety.
            /// </summary>
            public byte[] ImageData { get; internal set; }
    }

        public enum ICOImageType { Icon = 1, Cursor = 2 };

        /// <summary>
        /// The image type.
        /// 1 for icon (.ICO) image
        /// 2 for cursor (.CUR) image
        /// Other values are invalid.
        /// </summary>
        public ICOImageType ImageType { get; internal set; }

        /// <summary>
        /// The number of images in the file.
        /// </summary>
        public int NumImages {
            get {
                if (Images == null) {
                    return -1;
                } else {
                    return Images.Length;
                }
            }
        }
        public Image[] Images { get; internal set; }

        internal Icon () { }

        internal Icon(ICOImageType type, Image[] images) {
            ImageType = type;
            Images = images;
        }
    }
}