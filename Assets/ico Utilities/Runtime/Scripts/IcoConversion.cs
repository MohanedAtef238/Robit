using System;
using System.IO;
using UnityEngine;
using static Doji.Ico.Icon;

namespace Doji.Ico {

    public static class IcoConversion {

        /// <summary>
        /// The signature of png files.
        /// </summary>
        private static readonly byte[] PNG_HEADER = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        
        public static void EncodeToICO(this Texture2D tex, string fileName, IcoRescaleSettings settings = null) {
            using (Stream fileStream = File.OpenWrite(fileName)) {
                tex.EncodeToICO(fileStream, settings);
            }
        }
        public static void EncodeToICO(this Texture2D tex, Stream stream, IcoRescaleSettings settings = null) {
            using (BinaryWriter iconWriter = new BinaryWriter(stream)) {
                Icon icon = tex.InternalEncodeToICO(settings);
                icon.Serialize(iconWriter);
            }
        }
        public static byte[] EncodeToICO(this Texture2D tex, IcoRescaleSettings settings = null) {
            using (MemoryStream stream = new MemoryStream()) {
                tex.EncodeToICO(stream, settings);
                return stream.ToArray();
            }
        }

        private static Icon InternalEncodeToICO(this Texture2D tex, IcoRescaleSettings settings) {
            if (tex == null) {
                throw new ArgumentNullException(nameof(tex));
            }

            if (!IsSupported(tex.format)) {
                throw new ArgumentException($"Unsupported texture format ({tex.format}) - EncodeToICO only supports uncrompressed texture formats like RGBA32, ARGB32 and RGB24.");
            }

            if (settings == null) {
                settings = new IcoRescaleSettings();
            }

            Vector2Int targetResolution = settings.GetTargetResolution(tex.width, tex.height);
            Texture2D largestTexture = TextureUtility.CopyResize(tex, targetResolution);

            int numImages = settings.GetNumImages(tex.width, tex.height);
            Image[] images = new Image[numImages];

            Texture2D current = largestTexture;
            for (int i = 0; i < numImages; i++) {
                current = TextureUtility.CopyResize(tex, targetResolution);

                byte[] imageData = current.EncodeToPNG();
                images[i] = new Image() {
                    Width = current.width,
                    Height = current.height,
                    NumColors = 0,
                    ColorPlanes = 0,
                    BitsPerPixel = GetBitDepth(current.format),
                    ImageSize = imageData.Length,
                    Offset = -1,  // unimportant, filled later
                    ImageData = imageData
                };

                targetResolution /= 2;
            }

            return new Icon(ICOImageType.Icon, images);
        }

        private static void Serialize(this Icon icon, BinaryWriter iconWriter) {
            int offset = 0;
            byte O = 0;

            // reserved
            iconWriter.Write(O);
            iconWriter.Write(O);

            iconWriter.Write((short)icon.ImageType);
            iconWriter.Write((short)icon.NumImages);

            offset += 6 + (16 * icon.Images.Length);

            for (int i = 0; i < icon.Images.Length; i++) {
                iconWriter.Write(icon.Images[i]._width);
                iconWriter.Write(icon.Images[i]._height);
                iconWriter.Write(icon.Images[i].NumColors);
                iconWriter.Write(O);
                iconWriter.Write(icon.Images[i].ColorPlanes);
                iconWriter.Write(icon.Images[i].BitsPerPixel);
                iconWriter.Write(icon.Images[i].ImageData.Length);
                iconWriter.Write(offset);

                offset += icon.Images[i].ImageData.Length;
            }

            for (int i = 0; i < icon.Images.Length; i++) {
                // write image data
                iconWriter.Write(icon.Images[i].ImageData);
            }
        }

        private static bool IsSupported(TextureFormat format) {
            switch (format) {
                case TextureFormat.RGB24:
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                    return true;
                default:
                    return false;
            }
        }
        private static short GetBitDepth(TextureFormat format) {
            switch (format) {
                case TextureFormat.RGB24:
                    return 24;
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                    return 32;
                default:
                    return 0;
            }
        }

        public static Icon LoadIcon(string fileName) {
            using (Stream fileStream = File.OpenRead(fileName)) {
                return LoadIcon(fileStream);
            }
        }
        public static Icon LoadIcon(Stream stream) {
            using (BinaryReader iconReader = new BinaryReader(stream)) {
                return Deserialize(iconReader);
            }
        }
        public static Icon LoadIcon(byte[] array) {
            if (array == null) {
                throw new ArgumentNullException(nameof(array));
            }
            if (array.Length == 0) {
                throw new ArgumentException("Array can not be empty", nameof(array));
            }
            using (Stream stream = new MemoryStream(array)) {
                return LoadIcon(stream);
            }
        }

        /// <summary>
        /// An .ico file can contain either uncompressed bitmaps (BMPs) or PNGs.
        /// This method extracts a Texture2D object depending on which format is
        /// used by the given <paramref name="icon"/>.
        /// </summary>
        internal static Texture2D Decode(Icon icon, int index, bool mipChain, bool markNonReadable) {
            if (icon == null) {
                throw new ArgumentNullException(nameof(icon));
            }
            if (icon.NumImages == -1) {
                throw new ArgumentException("The icon does not contain any images", nameof(icon));
            }
            if (index >= icon.NumImages) {
                throw new ArgumentException("The icon does not contain an image for the given index", nameof(index));
            }

            var img = icon.Images[index];
            bool isPNG = CheckPNGHeader(img.ImageData);
            Texture2D texture;

            if (isPNG) {
                texture = new Texture2D(img.Width, img.Height, TextureFormat.ARGB32, mipChain);
                ImageConversion.LoadImage(texture, img.ImageData, markNonReadable);
                return texture;
            } else {
                ICOBMPLoader bmpLoader = new ICOBMPLoader();
                bmpLoader.ReadAsIcoBitmap = true;
                bmpLoader.ForceAlphaReadWhenPossible = true;
                bmpLoader.ReadPaletteAlpha = true;
                var bmp = bmpLoader.LoadBMP(img.ImageData);
                texture = bmp.ToTexture2D(mipChain, markNonReadable);
            }

            texture.wrapMode = TextureWrapMode.Clamp;

            return texture;
        }

        public static Icon Deserialize(BinaryReader iconReader) {
            Icon icon = new Icon();

            // reserved
            iconReader.ReadByte();
            iconReader.ReadByte();

            short imageType = iconReader.ReadInt16();
            if (imageType != 1 && imageType != 2) {
                throw new InvalidDataException("Not a valid ICO image type: " + imageType);
            }
            icon.ImageType = (ICOImageType)imageType;

            short numImages = iconReader.ReadInt16();
            icon.Images = new Image[numImages];

            for (int i = 0; i < numImages; i++) {
                byte width        = iconReader.ReadByte();
                byte height       = iconReader.ReadByte();
                byte numColors    = iconReader.ReadByte();
                byte reserved     = iconReader.ReadByte();
                short colorPlanes = iconReader.ReadInt16();
                short bitsPerPx   = iconReader.ReadInt16();
                int imageSize     = iconReader.ReadInt32();
                int offset        = iconReader.ReadInt32();

                icon.Images[i] = new Image() {
                    Width = width,
                    Height = height,
                    NumColors = numColors,
                    ColorPlanes = colorPlanes,
                    BitsPerPixel = bitsPerPx,
                    ImageSize = imageSize,
                    Offset = offset
                };
            }

            for (int i = 0; i < numImages; i++) {
                iconReader.BaseStream.Seek(icon.Images[i].Offset, SeekOrigin.Begin);
                icon.Images[i].ImageData = iconReader.ReadBytes(icon.Images[i].ImageSize);
            }
            return icon;
        }

        /// <summary>
        /// Returns whether the given <paramref name="array"/>
        /// starts with the PNG file signature.
        /// </summary>
        private static bool CheckPNGHeader(byte[] array) {
            if (array == null || array.Length < 8) {
                return false;
            }

            for (int i = 0; i < 8; i++) {
                if (array[i] != PNG_HEADER[i]) {
                    return false;
                }
            }

            return true;
        }
    }
}