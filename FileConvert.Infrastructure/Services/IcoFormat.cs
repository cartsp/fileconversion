using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileConvert.Infrastructure
{
    /// <summary>
    /// Helper class for reading and writing ICO (Windows Icon) format files.
    /// ICO format specification: https://docs.fileformat.com/image/ico/
    /// </summary>
    internal static class IcoFormat
    {
        // ICO file header
        private const ushort IconType = 1;  // 1 = ICO, 2 = CUR
        private const int IconDirHeaderSize = 6;
        private const int IconDirEntrySize = 16;
        private const int MaxImageCount = 256;
        private const int MaxImageSize = 64 * 1024 * 1024; // 64MB max

        /// <summary>
        /// Encodes an image as an ICO file with embedded PNG data.
        /// Modern ICO files can contain PNG data which is more efficient.
        /// </summary>
        public static void EncodeAsIco(Image image, Stream output)
        {
            using var writer = new BinaryWriter(output, System.Text.Encoding.Default, leaveOpen: true);

            // Determine dimensions (ICO supports up to 256x256, but width/height are stored as 1 byte each)
            // If 256, store as 0 (special case)
            byte width = image.Width >= 256 ? (byte)0 : (byte)image.Width;
            byte height = image.Height >= 256 ? (byte)0 : (byte)image.Height;

            // Encode image as PNG
            using var pngStream = new MemoryStream();
            image.SaveAsPng(pngStream);
            var pngData = pngStream.ToArray();

            // Write ICONDIR header
            writer.Write((ushort)0);       // Reserved, must be 0
            writer.Write(IconType);        // Type: 1 = ICO
            writer.Write((ushort)1);       // Number of images

            // Write ICONDIRENTRY
            writer.Write(width);           // Width (0 = 256)
            writer.Write(height);          // Height (0 = 256)
            writer.Write((byte)0);         // Color palette (0 = no palette)
            writer.Write((byte)0);         // Reserved
            writer.Write((ushort)1);       // Color planes
            writer.Write((ushort)32);      // Bits per pixel
            writer.Write((uint)pngData.Length);  // Size of image data
            writer.Write((uint)(IconDirHeaderSize + IconDirEntrySize));  // Offset to image data

            // Write PNG data
            writer.Write(pngData);
        }

        /// <summary>
        /// Decodes an ICO file and returns the first (or largest) image.
        /// Supports both PNG and BMP embedded formats.
        /// </summary>
        public static Image DecodeFromIco(Stream input)
        {
            using var reader = new BinaryReader(input, System.Text.Encoding.Default, leaveOpen: true);

            // Read ICONDIR header
            var reserved = reader.ReadUInt16();
            if (reserved != 0)
                throw new InvalidDataException("Invalid ICO file: reserved field must be 0");

            var type = reader.ReadUInt16();
            if (type != IconType)
                throw new InvalidDataException($"Invalid ICO file: expected type 1, got {type}");

            var imageCount = reader.ReadUInt16();
            if (imageCount == 0)
                throw new InvalidDataException("Invalid ICO file: no images found");
            if (imageCount > MaxImageCount)
                throw new InvalidDataException($"Invalid ICO file: too many images ({imageCount}), max is {MaxImageCount}");

            // Read all entries to find the largest/best one
            var entries = new IconDirEntry[imageCount];
            int maxWidth = 0;
            int bestIndex = 0;

            for (int i = 0; i < imageCount; i++)
            {
                entries[i] = new IconDirEntry
                {
                    Width = reader.ReadByte(),
                    Height = reader.ReadByte(),
                    ColorCount = reader.ReadByte(),
                    Reserved = reader.ReadByte(),
                    Planes = reader.ReadUInt16(),
                    BitsPerPixel = reader.ReadUInt16(),
                    Size = reader.ReadUInt32(),
                    Offset = reader.ReadUInt32()
                };

                // Width/Height of 0 means 256
                int actualWidth = entries[i].Width == 0 ? 256 : entries[i].Width;
                if (actualWidth > maxWidth)
                {
                    maxWidth = actualWidth;
                    bestIndex = i;
                }
            }

            var bestEntry = entries[bestIndex];

            // Validate offset before seeking
            var minValidOffset = IconDirHeaderSize + imageCount * IconDirEntrySize;
            if (bestEntry.Offset < minValidOffset || bestEntry.Offset >= input.Length)
                throw new InvalidDataException($"Invalid ICO file: image offset {bestEntry.Offset} is out of bounds");

            // Validate size before reading
            if (bestEntry.Size > MaxImageSize)
                throw new InvalidDataException($"Invalid ICO file: image data too large ({bestEntry.Size} bytes), max is {MaxImageSize} bytes");
            if (bestEntry.Offset + bestEntry.Size > input.Length)
                throw new InvalidDataException("Invalid ICO file: image data extends beyond file bounds");

            // Seek to image data
            input.Position = bestEntry.Offset;

            // Read image data
            var imageData = reader.ReadBytes((int)bestEntry.Size);

            // Check if it's PNG (starts with PNG signature)
            if (imageData.Length >= 8 && imageData[0] == 0x89 && imageData[1] == 0x50 &&
                imageData[2] == 0x4E && imageData[3] == 0x47)
            {
                // It's PNG data
                using var pngStream = new MemoryStream(imageData);
                return Image.Load(pngStream);
            }
            else
            {
                // It's BMP data - parse and convert
                return DecodeBmpIconData(imageData, bestEntry.Width == 0 ? 256 : bestEntry.Width,
                    bestEntry.Height == 0 ? 256 : bestEntry.Height, bestEntry.BitsPerPixel);
            }
        }

        /// <summary>
        /// Decodes BMP-format icon data. This is more complex as ICO stores BMP data
        /// with some differences from standard BMP files.
        /// </summary>
        private static Image DecodeBmpIconData(byte[] data, int width, int height, ushort bitsPerPixel)
        {
            // BMP icon data starts with BITMAPINFOHEADER (40 bytes)
            using var dataStream = new MemoryStream(data);
            using var reader = new BinaryReader(dataStream);

            // Read BITMAPINFOHEADER
            var headerSize = reader.ReadUInt32();
            if (headerSize != 40 && headerSize != 108)
                throw new InvalidDataException($"Unsupported BMP header size: {headerSize}, expected 40 or 108");
            var bmpWidth = (int)reader.ReadInt32();
            var bmpHeight = (int)reader.ReadInt32() / 2;  // ICO stores double height (XOR + AND masks)
            var planes = reader.ReadUInt16();
            var bpp = reader.ReadUInt16();
            var compression = reader.ReadUInt32();
            var imageSize = reader.ReadInt32();
            var xPelsPerMeter = reader.ReadInt32();
            var yPelsPerMeter = reader.ReadInt32();
            var colorsUsed = reader.ReadUInt32();
            var colorsImportant = reader.ReadUInt32();

            // Read color palette if present (for 8-bit or less)
            int colorsInPalette = bpp <= 8 ? (colorsUsed == 0 ? 1 << bpp : (int)colorsUsed) : 0;
            var palette = new Rgba32[colorsInPalette];
            for (int i = 0; i < colorsInPalette; i++)
            {
                var b = reader.ReadByte();
                var g = reader.ReadByte();
                var r = reader.ReadByte();
                var reserved = reader.ReadByte();
                palette[i] = new Rgba32(r, g, b, 255);
            }

            // Calculate row stride (rows are padded to 4-byte boundaries)
            int bytesPerPixel = (bpp + 7) / 8;
            int rowStride = ((bmpWidth * bytesPerPixel + 3) / 4) * 4;
            int pixelDataSize = rowStride * bmpHeight;

            // Validate we have enough data for pixels
            if (dataStream.Position + pixelDataSize > dataStream.Length)
                throw new InvalidDataException($"Invalid ICO file: insufficient pixel data (need {pixelDataSize} bytes, have {dataStream.Length - dataStream.Position})");

            // Read pixel data (XOR mask)
            var pixels = new byte[pixelDataSize];
            int pixelsRead = dataStream.Read(pixels, 0, pixelDataSize);
            if (pixelsRead != pixelDataSize)
                throw new InvalidDataException($"Invalid ICO file: failed to read pixel data (expected {pixelDataSize}, got {pixelsRead})");

            // Read AND mask (1-bit transparency mask)
            int andMaskStride = ((bmpWidth + 31) / 32) * 4;
            int andMaskSize = andMaskStride * bmpHeight;

            // Validate we have enough data for AND mask
            if (dataStream.Position + andMaskSize > dataStream.Length)
                throw new InvalidDataException($"Invalid ICO file: insufficient AND mask data (need {andMaskSize} bytes, have {dataStream.Length - dataStream.Position})");

            var andMask = new byte[andMaskSize];
            int andMaskRead = dataStream.Read(andMask, 0, andMaskSize);
            if (andMaskRead != andMaskSize)
                throw new InvalidDataException($"Invalid ICO file: failed to read AND mask data (expected {andMaskSize}, got {andMaskRead})");

            // Create output image
            var image = new Image<Rgba32>(bmpWidth, bmpHeight);

            // Decode pixels (BMP stores rows bottom-to-top)
            for (int y = 0; y < bmpHeight; y++)
            {
                int srcY = bmpHeight - 1 - y;  // Flip vertically
                for (int x = 0; x < bmpWidth; x++)
                {
                    Rgba32 color;

                    if (bpp == 32)
                    {
                        int offset = srcY * rowStride + x * 4;
                        if (offset + 3 >= pixels.Length)
                            throw new InvalidDataException($"Invalid ICO file: pixel data overflow at ({x}, {y})");
                        color = new Rgba32(
                            pixels[offset + 2],  // R
                            pixels[offset + 1],  // G
                            pixels[offset + 0],  // B
                            pixels[offset + 3]   // A
                        );
                    }
                    else if (bpp == 24)
                    {
                        int offset = srcY * rowStride + x * 3;
                        if (offset + 2 >= pixels.Length)
                            throw new InvalidDataException($"Invalid ICO file: pixel data overflow at ({x}, {y})");
                        // Check AND mask for transparency
                        bool transparent = IsBitSet(andMask, srcY * andMaskStride, x);
                        color = new Rgba32(
                            pixels[offset + 2],  // R
                            pixels[offset + 1],  // G
                            pixels[offset + 0],  // B
                            transparent ? (byte)0 : (byte)255
                        );
                    }
                    else if (bpp == 8)
                    {
                        int offset = srcY * rowStride + x;
                        if (offset >= pixels.Length)
                            throw new InvalidDataException($"Invalid ICO file: pixel data overflow at ({x}, {y})");
                        byte paletteIndex = pixels[offset];
                        bool transparent = IsBitSet(andMask, srcY * andMaskStride, x);
                        color = paletteIndex < palette.Length
                            ? new Rgba32(palette[paletteIndex].R, palette[paletteIndex].G, palette[paletteIndex].B,
                                transparent ? (byte)0 : (byte)255)
                            : new Rgba32(0, 0, 0, 255);
                    }
                    else
                    {
                        // Fallback for other bit depths
                        color = new Rgba32(0, 0, 0, 255);
                    }

                    image[x, y] = color;
                }
            }

            return image;
        }

        private static bool IsBitSet(byte[] data, int rowStart, int bitIndex)
        {
            int byteIndex = rowStart + bitIndex / 8;
            int bitPosition = 7 - (bitIndex % 8);
            return byteIndex < data.Length && (data[byteIndex] & (1 << bitPosition)) != 0;
        }

        private struct IconDirEntry
        {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitsPerPixel;
            public uint Size;
            public uint Offset;
        }
    }
}
