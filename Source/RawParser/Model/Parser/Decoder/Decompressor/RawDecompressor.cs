using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RawNet.Decoder.Decompressor
{
    static class RawDecompressor
    {
        /** Attempt to decode the image 
         * A RawDecoderException will be thrown if the image cannot be decoded
         */
        public static void ReadUncompressedRaw(TiffBinaryReader input, Point2D size, Point2D offset, long inputPitch, int bitPerPixel, BitOrder order, RawImage<ushort> rawImage)
        {
            //uint outPitch = rawImage.pitch;
            long width = size.width;
            long height = size.height;

            if (input.RemainingSize < (inputPitch * height))
            {
                if (input.RemainingSize > inputPitch)
                {
                    height = input.RemainingSize / inputPitch - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            if (bitPerPixel > 16)
                throw new RawDecoderException("Unsupported bit depth");

            int skipBits = (int)(inputPitch - width * rawImage.raw.cpp * bitPerPixel / 8);  // Skip per line
            if (offset.height > rawImage.raw.dim.height)
                throw new RawDecoderException("Invalid y offset");
            if (offset.width + size.width > rawImage.raw.dim.width)
                throw new RawDecoderException("Invalid x offset");

            uint y = offset.height;

            if (bitPerPixel == 8)
            {
                Decode12BitRaw(input, width, height, rawImage);
                return;
            }

            width *= rawImage.raw.cpp;
            var off = ((width * bitPerPixel) / 8) + skipBits;
            var pumps = new BitPump[height];

            //read the data
            switch (order)
            {
                case BitOrder.Jpeg:
                    for (int i = 0; i < height; i++)
                    {
                        pumps[i] = new BitPumpMSB(input, input.BaseStream.Position, off);
                    }
                    break;
                case BitOrder.Jpeg16:
                    for (int i = 0; i < height; i++)
                    {
                        pumps[i] = new BitPumpMSB16(input, input.BaseStream.Position, off);
                    }
                    break;
                case BitOrder.Jpeg32:
                    for (int i = 0; i < height; i++)
                    {
                        pumps[i] = new BitPumpMSB32(input, input.BaseStream.Position, off);
                    }
                    break;
                default:
                    for (int i = 0; i < height; i++)
                    {
                        pumps[i] = new BitPumpPlain(input, input.BaseStream.Position, off);
                    }
                    break;
            }
            Parallel.For(0, height, i =>
            {
                long pos = ((offset.height + i) * rawImage.raw.dim.width + offset.width) * rawImage.raw.cpp;
                for (uint x = 0; x < width; x++)
                {
                    rawImage.raw.rawView[x + pos] = (ushort)pumps[i].GetBits(bitPerPixel);
                }
            });
        }

        public static void Decode8BitRaw(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            var count = width * height;
            if (input.RemainingSize < count)
            {
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

        public static void Decode12BitRaw(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            if (input.RemainingSize < ((width * 12 / 8) * height) && input.RemainingSize > (width * 12 / 8))
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                var skip = (y * rawImage.raw.UncroppedDim.width);
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

        public static void Decode12BitRawWithControl(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            // Calulate expected bytes per line.
            long perline = (width * 12 / 8);
            // Add skips every 10 pixels
            perline += ((width + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.RemainingSize < (perline * height))
            {
                throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[(y * rawImage.raw.dim.width) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[(y * rawImage.raw.dim.width) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        public static void Decode12BitRawBEWithControl(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            // Calulate expected bytes per line.
            long perline = (width * 12 / 8);
            // Add skips every 10 pixels
            perline += ((width + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.RemainingSize < (perline * height))
            {
                throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                var skip = (y * rawImage.raw.dim.width);
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

        public static void Decode12BitRawBE(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.width + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.width + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEInterlaced(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            long half = (height + 1) >> 1;
            long y = 0;
            for (int row = 0; row < height; row++)
            {
                y = row % half * 2 + row / half;
                var skip = y * rawImage.raw.dim.width;
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
                    rawImage.raw.rawView[skip + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEunpacked(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            if (input.RemainingSize < width * height * 2)
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.width + x] = (ushort)(((g1 & 0x0f) << 8) | g2);
                }
            }
        }

        public static void Decode12BitRawBEunpackedLeftAligned(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            // uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                var skip = y * rawImage.raw.dim.width;
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        public static void Decode14BitRawBEunpacked(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            // uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                var skip = y * rawImage.raw.dim.width;
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 & 0x3f) << 8) | g2);
                }
            }
        }

        public static void Decode16BitRawUnpacked(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            //uint pitch = rawImage.pitch;
            if (input.RemainingSize < width * height * 2)
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                var skip = y * rawImage.raw.dim.width;
                for (int x = 0; x < width; x += 1)
                {
                    rawImage.raw.rawView[skip + x] = input.ReadUInt16();
                }
            }
        }

        public static void Decode12BitRawUnpacked(TiffBinaryReader input, long width, long height, RawImage<ushort> rawImage)
        {
            if (input.RemainingSize < width * height * 2)
            {
                throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[y * rawImage.raw.dim.width + x] = (ushort)(((g2 << 8) | g1) >> 4);
                }
            }
        }
    }
}
