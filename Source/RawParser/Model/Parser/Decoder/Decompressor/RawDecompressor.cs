using RawNet.Format.TIFF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RawNet.RawDecoder;

namespace RawNet.Decoder.Decompressor
{
    static class RawDecompressor
    {
        /** Attempt to decode the image 
         * A RawDecoderException will be thrown if the image cannot be decoded
         */
        public static void ReadUncompressedRaw(TIFFBinaryReader input, Point2D size, Point2D offset, long inputPitch, int bitPerPixel, BitOrder order, RawImage rawImage)
        {
            //uint outPitch = rawImage.pitch;
            long w = size.Width;
            long h = size.Height;

            if (input.RemainingSize < (inputPitch * h))
            {
                if (input.RemainingSize > inputPitch)
                {
                    h = input.RemainingSize / inputPitch - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            if (bitPerPixel > 16)
                throw new RawDecoderException("Unsupported bit depth");

            int skipBits = (int)(inputPitch - w * rawImage.cpp * bitPerPixel / 8);  // Skip per line
            if (offset.Height > rawImage.raw.dim.Height)
                throw new RawDecoderException("Invalid y offset");
            if (offset.Width + size.Width > rawImage.raw.dim.Width)
                throw new RawDecoderException("Invalid x offset");

            uint y = offset.Height;
            h = Math.Min(h + offset.Height, rawImage.raw.dim.Height);

            /*if (mRaw.getDataType() == TYPE_FLOAT32)
            {
                if (bitPerPixel != 32)
                    throw new RawDecoderException("readUncompressedRaw: Only 32 bit float point supported");
                BitBlt(&data[offset.x * sizeof(float) * cpp + y * outPitch], outPitch,
                    input.getData(), inputPitch, w * mRaw.bpp, h - y);
                return;
            }*/
            BitPump bits;
            switch (order)
            {
                case BitOrder.Jpeg:
                    bits = new BitPumpMSB(input);
                    break;
                case BitOrder.Jpeg16:
                    bits = new BitPumpMSB16(input);
                    break;
                case BitOrder.Jpeg32:
                    bits = new BitPumpMSB32(input);
                    break;
                default:
                    if (bitPerPixel == 16 && Common.GetHostEndianness() == Endianness.Little)
                    {
                        Decode16BitRawUnpacked(input, w, h, rawImage);
                        return;
                    }
                    if (bitPerPixel == 12 && w == inputPitch * 8 / 12 && Common.GetHostEndianness() == Endianness.Little)
                    {
                        Decode12BitRaw(input, w, h, rawImage);
                        return;
                    }
                    bits = new BitPumpPlain(input);
                    break;
            }

            w *= rawImage.cpp;
            for (; y < h; y++)
            {
                bits.CheckPos();
                var skip = (offset.Width + y * rawImage.raw.dim.Width) * rawImage.cpp;
                for (uint x = 0; x < w; x++)
                {
                    uint b = bits.GetBits(bitPerPixel);
                    rawImage.raw.rawView[x + skip] = (ushort)b;
                }
                bits.SkipBits(skipBits);
            }
        }

        public static void Decode8BitRaw(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            var count = width * height;
            if (input.RemainingSize < count)
            {
                if (input.RemainingSize > width)
                {
                    height = input.RemainingSize / width - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Decode8BitRaw: Not enough data to decode a single line. Image file truncated.");
            }

            var temp = input.ReadBytes((int)count);
            Parallel.For(0, height, y =>
            {
                var skip = y * width;
                for (int x = 0; x < width; x++)
                {
                    rawImage.raw.rawView[skip + x] = temp[skip + x];
                }
            });
        }

        public static void Decode12BitRaw(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (width < 2) throw new IOException("1 pixel wide raw images are not supported");
            //uint pitch = rawImage.pitch;

            if (input.RemainingSize < ((width * 12 / 8) * height) && input.RemainingSize > (width * 12 / 8))
            {
                height = input.RemainingSize / (width * 12 / 8) - 1;
                rawImage.errors.Add("Image truncated (file is too short)");
            }
            else
                throw new IOException("Not enough data to decode a single line. Image file truncated.");

            for (int y = 0; y < height; y++)
            {
                var skip = (y * rawImage.raw.uncroppedDim.Width);
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                }
            }
        }

        public static void Decode12BitRawWithControl(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");
            //uint pitch = rawImage.pitch;

            // Calulate expected bytes per line.
            long perline = (width * 12 / 8);
            // Add skips every 10 pixels
            perline += ((width + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.RemainingSize < (perline * height))
            {
                if (input.RemainingSize > perline)
                {
                    height = input.RemainingSize / perline - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                {
                    throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[(y * rawImage.raw.dim.Width) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[(y * rawImage.raw.dim.Width) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        public static void Decode12BitRawBEWithControl(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");
            // Calulate expected bytes per line.
            long perline = (width * 12 / 8);
            // Add skips every 10 pixels
            perline += ((width + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.RemainingSize < (perline * height))
            {
                if (input.RemainingSize > perline)
                {
                    height = input.RemainingSize / perline - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                {
                    throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }
            for (int y = 0; y < height; y++)
            {
                var skip = (y * rawImage.raw.dim.Width);
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        public static void Decode12BitRawBE(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                if (input.RemainingSize > (width * 12 / 8))
                {
                    height = (input.RemainingSize / (width * 12 / 8) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEInterlaced(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                if (input.RemainingSize > (width * 12 / 8))
                {
                    height = input.RemainingSize / (width * 12 / 8) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            long half = (height + 1) >> 1;
            long y = 0;
            for (int row = 0; row < height; row++)
            {
                y = row % half * 2 + row / half;
                if (y == 1)
                {
                    // The second field starts at a 2048 byte aligment
                    long offset = ((half * width * 3 / 2 >> 11) + 1) << 11;
                    if (offset > input.RemainingSize)
                        throw new IOException("Decode12BitSplitRaw: Trying to jump to invalid offset " + offset);
                    input.Position = offset;
                }
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEunpacked(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = input.RemainingSize / (width * 2) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)(((g1 & 0x0f) << 8) | g2);
                }
            }
        }

        public static void Decode12BitRawBEunpackedLeftAligned(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            // uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = input.RemainingSize / (width * 2) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                var skip = y * rawImage.raw.dim.Width;
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        public static void Decode14BitRawBEunpacked(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            // uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = input.RemainingSize / (width * 2) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                var skip = y * rawImage.raw.dim.Width;
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 & 0x3f) << 8) | g2);
                }
            }
        }

        public static void Decode16BitRawUnpacked(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            //uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = (input.RemainingSize / (width * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)((g2 << 8) | g1);
                }
            }
        }

        public static void Decode16BitRawBEunpacked(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            // uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = input.RemainingSize / (width * 2) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)((g1 << 8) | g2);
                }
            }
        }

        public static void Decode12BitRawUnpacked(TIFFBinaryReader input, long width, long height, RawImage rawImage)
        {
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = (input.RemainingSize / (width * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.Width + x] = (ushort)(((g2 << 8) | g1) >> 4);
                }
            }
        }
    }
}
