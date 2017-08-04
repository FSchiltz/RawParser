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
            var pos = new Point2D(size);//to avoid rewriting the image pos

            if (input.RemainingSize < (inputPitch * pos.height))
            {
                if (input.RemainingSize > inputPitch)
                {
                    pos.height = (uint)(input.RemainingSize / inputPitch - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }
            if (bitPerPixel > 16)
                throw new RawDecoderException("Unsupported bit depth");

            int skipBits = (int)(inputPitch - pos.width * rawImage.raw.cpp * bitPerPixel / 8);  // Skip per line
            if (offset.height > rawImage.raw.dim.height)
                throw new RawDecoderException("Invalid y offset");
            if (offset.width + size.width > rawImage.raw.dim.width)
                throw new RawDecoderException("Invalid x offset");

            uint y = offset.height;

            if (bitPerPixel == 8)
            {
                Decode8BitRaw(input, pos, offset, rawImage);
                return;
            }
            else if (bitPerPixel == 10 && order == BitOrder.Jpeg && skipBits == 0)
            {
                //Optimisation for windows phone DNG
                Decode10BitRaw(input, pos, offset, rawImage);
                return;
            } /*
            else if (bitPerPixel == 16 && Common.GetHostEndianness() == Endianness.Little)
            {
                Decode16BitRawUnpacked(input, pos, offset, rawImage);
                return;
            }
            else if (bitPerPixel == 12 && pos.width == inputPitch * 8 / 12 && Common.GetHostEndianness() == Endianness.Little)
            {
                Decode12BitRaw(input, pos, offset, rawImage);
                return;
            }*/

            pos.width *= rawImage.raw.cpp;
            var off = ((pos.width * bitPerPixel) / 8) + skipBits;
            var pumps = new BitPump[pos.height];

            //read the data
            switch (order)
            {
                case BitOrder.Jpeg:
                    for (int i = 0; i < pos.height; i++)
                    {
                        pumps[i] = new BitPumpMSB(input, input.BaseStream.Position, off);
                    }
                    break;
                case BitOrder.Jpeg16:
                    for (int i = 0; i < pos.height; i++)
                    {
                        pumps[i] = new BitPumpMSB16(input, input.BaseStream.Position, off);
                    }
                    break;
                case BitOrder.Jpeg32:
                    for (int i = 0; i < pos.height; i++)
                    {
                        pumps[i] = new BitPumpMSB32(input, input.BaseStream.Position, off);
                    }
                    break;
                default:
                    for (int i = 0; i < pos.height; i++)
                    {
                        pumps[i] = new BitPumpPlain(input, input.BaseStream.Position, off);
                    }
                    break;
            }
            Parallel.For(0, pos.height, i =>
            {
                long p = ((offset.height + i) * rawImage.raw.dim.width + offset.width) * rawImage.raw.cpp;
                for (uint x = 0; x < pos.width; x++)
                {
                    rawImage.raw.rawView[x + p] = (ushort)pumps[i].GetBits(bitPerPixel);
                }
            });
        }

        public static void Decode8BitRaw(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {

            var temp = input.ReadBytes((int)size.Area);
            Parallel.For(0, size.height, y =>
            {
                var skip = (y + offset.height) * rawImage.raw.UncroppedDim.width + offset.width;
                for (int x = 0; x < size.width; x++)
                {
                    rawImage.raw.rawView[skip + x] = temp[skip + x];
                }
            });
        }

        public static void Decode10BitRaw(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            int off = (int)(size.width * 10) / 8;
            var pumps = new byte[size.height][];

            //read the data
            for (int i = 0; i < size.height; i++)
            {
                pumps[i] = input.ReadBytes(off);
            }

            Parallel.For(0, size.height, i =>
            {
                long pos = (i + offset.height) * rawImage.raw.dim.width + offset.width;
                var data = pumps[i];
                var bytePos = 0;
                for (uint x = 0; x < size.width;)
                {
                    rawImage.raw.rawView[pos + x++] = (ushort)((data[bytePos++] << 2) + (data[bytePos] >> 6));
                    rawImage.raw.rawView[pos + x++] = (ushort)(((data[bytePos++] & 63) << 4) + (data[bytePos] >> 4));
                    rawImage.raw.rawView[pos + x++] = (ushort)(((data[bytePos++] & 15) << 6) + (data[bytePos] >> 2));
                    rawImage.raw.rawView[pos + x++] = (ushort)(((data[bytePos++] & 3) << 8) + data[bytePos++]);
                }
                pumps[i] = null;
            });
        }

        public static void Decode12BitRaw(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.UncroppedDim.width + offset.width;
                for (int x = 0; x < size.width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)((g1 << 4) | (g2 & 0xf));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)(((g2 & 0xf) >> 4) | g3);
                }
            }
        }

        public static void Decode12BitRawWithControl(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                }
            }
        }

        public static void Decode12BitRawBEWithControl(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBE(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var pos = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[pos + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[pos + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEInterlaced(TiffBinaryReader input, Point2D size, Point2D off, RawImage<ushort> rawImage)
        {
            long half = (size.height + 1) >> 1;
            long y = 0;
            for (int row = 0; row < size.height; row++)
            {
                y = row % half * 2 + row / half;
                var skip = (y + off.height) * rawImage.raw.dim.width + off.width;
                if (y == 1)
                {
                    // The second field starts at a 2048 byte aligment
                    long offset = ((half * size.width * 3 / 2 >> 11) + 1) << 11;
                    if (offset > input.RemainingSize)
                        throw new IOException("Decode12BitSplitRaw: Trying to jump to invalid offset " + offset);
                    input.Position = offset;
                }
                for (int x = 0; x < size.width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.rawView[skip + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        public static void Decode12BitRawBEunpacked(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var pos = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[pos + x] = (ushort)(((g1 & 0x0f) << 8) | g2);
                }
            }
        }

        public static void Decode12BitRawBEunpackedLeftAligned(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.dim.width + offset.height;
                for (int x = 0; x < size.width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        public static void Decode14BitRawBEunpacked(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[skip + x] = (ushort)(((g1 & 0x3f) << 8) | g2);
                }
            }
        }

        public static void Decode16BitRawUnpacked(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var skip = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 1)
                {
                    rawImage.raw.rawView[skip + x] = input.ReadUInt16();
                }
            }
        }

        public static void Decode12BitRawUnpacked(TiffBinaryReader input, Point2D size, Point2D offset, RawImage<ushort> rawImage)
        {
            for (int y = 0; y < size.height; y++)
            {
                var pos = (y + offset.height) * rawImage.raw.dim.width + offset.width;
                for (int x = 0; x < size.width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.rawView[pos + x] = (ushort)(((g2 << 8) | g1) >> 4);
                }
            }
        }
    }
}
