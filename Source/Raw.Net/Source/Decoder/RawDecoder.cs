using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    //TODO fix comment from original
    public abstract class RawDecoder
    {
        internal class RawSlice
        {
            public RawSlice() { }
            public UInt32 h = 0;
            public UInt32 offset = 0;
            public UInt32 count = 0;
        }

        /* The decoded image - undefined if image has not or could not be decoded. */
        public RawImage rawImage;

        /* Apply stage 1 DNG opcodes. */
        /* This usually maps out bad pixels, etc */
        protected bool ApplyStage1DngOpcodes { get; set; }

        /* Should Fuji images be rotated? */
        protected bool FujiRotate { get; set; }

        /* The Raw input file to be decoded */
        protected TIFFBinaryReader reader;

        /* Hints set for the camera after checkCameraSupported has been called from the implementation*/
        protected Dictionary<string, string> hints = new Dictionary<string, string>();

        protected Stream stream;

        /* Construct decoder instance - FileMap is a filemap of the file to be decoded */
        /* The FileMap is not owned by this class, will not be deleted, and must remain */
        /* valid while this object exists */
        protected RawDecoder(ref Stream stream)
        {
            this.stream = stream;
            rawImage = new RawImage();
            ApplyStage1DngOpcodes = true;
            FujiRotate = true;
        }

        /*
         * return a byte[] containing an JPEG image or null if the file doesn't have a thumbnail
         */
        public virtual Thumbnail DecodeThumb() { return null; }

        /* Attempt to decode the image */
        /* A RawDecoderException will be thrown if the image cannot be decoded, */
        /* and there will not be any data in the mRaw image. */
        /* This function must be overridden by actual decoders. */
        public abstract void DecodeRaw();

        public abstract void DecodeMetadata();

        /** 
         * Check if the decoder can decode the image from this camera 
         A RawDecoderException will be thrown if the camera isn't supported 
         Unknown cameras does NOT generate any specific feedback 
         This function must be overridden by actual decoders */
        internal void DecodeUncompressed(ref IFD rawIFD, BitOrder order)
        {
            UInt32 nslices = rawIFD.GetEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = rawIFD.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = rawIFD.GetEntry(TagType.STRIPBYTECOUNTS);
            UInt32 yPerSlice = rawIFD.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
            Int32 width = rawIFD.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            UInt32 height = rawIFD.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            int bitPerPixel = rawIFD.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);

            List<RawSlice> slices = new List<RawSlice>();
            UInt32 offY = 0;

            for (UInt32 s = 0; s < nslices; s++)
            {
                RawSlice slice = new RawSlice()
                {
                    offset = (uint)offsets.data[s],
                    count = (uint)counts.data[s]
                };
                if (offY + yPerSlice > height)
                    slice.h = height - offY;
                else
                    slice.h = yPerSlice;

                offY += yPerSlice;

                if (reader.IsValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            if (0 == slices.Count)
                throw new RawDecoderException("RAW Decoder: No valid slices found. File probably truncated.");

            rawImage.dim.width = width;
            rawImage.dim.height = (int)offY;
            rawImage.whitePoint = (uint)(1 << bitPerPixel) - 1;

            offY = 0;
            for (int i = 0; i < slices.Count; i++)
            {
                RawSlice slice = slices[i];
                TIFFBinaryReader input;
                if (reader is TIFFBinaryReaderRE) input = new TIFFBinaryReaderRE(reader.BaseStream, slice.offset);
                else input = new TIFFBinaryReader(reader.BaseStream, slice.offset);
                Point2D size = new Point2D(width, (int)slice.h);
                Point2D pos = new Point2D(0, (int)offY);
                bitPerPixel = (int)(slice.count * 8u / (slice.h * width));
                try
                {
                    ReadUncompressedRaw(ref input, size, pos, width * bitPerPixel / 8, bitPerPixel, order);
                }
                catch (RawDecoderException)
                {
                    if (i > 0)
                    {
                        //TODO add something
                    }
                    else
                        throw;
                }
                catch (IOException e)
                {
                    if (i > 0)
                    {
                        //TODO add something
                    }
                    else
                        throw new RawDecoderException("RAW decoder: IO error occurred in first slice, unable to decode more. Error is: " + e);
                }
                offY += slice.h;
            }
        }

        /** Attempt to decode the image 
         * A RawDecoderException will be thrown if the image cannot be decoded
         */
        protected unsafe void ReadUncompressedRaw(ref TIFFBinaryReader input, Point2D size, Point2D offset, int inputPitch, int bitPerPixel, BitOrder order)
        {
            fixed (ushort* d = rawImage.rawData)
            {
                byte* data = (byte*)d;
                uint outPitch = rawImage.pitch;
                int w = size.width;
                int h = size.height;
                uint cpp = rawImage.cpp;
                int ox = offset.width;
                int oy = offset.height;

                if (input.GetRemainSize() < (inputPitch * h))
                {
                    if ((int)input.GetRemainSize() > inputPitch)
                    {
                        h = input.GetRemainSize() / inputPitch - 1;
                        rawImage.errors.Add("Image truncated (file is too short)");
                    }
                    else
                        throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
                }
                if (bitPerPixel > 16)
                    throw new RawDecoderException("readUncompressedRaw: Unsupported bit depth");

                uint skipBits = (uint)(inputPitch - w * cpp * bitPerPixel / 8);  // Skip per line
                if (oy > rawImage.dim.height)
                    throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
                if (ox + size.width > rawImage.dim.width)
                    throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

                int y = oy;
                h = (int)Math.Min(h + oy, (uint)rawImage.dim.height);

                /*if (mRaw.getDataType() == TYPE_FLOAT32)
                {
                    if (bitPerPixel != 32)
                        throw new RawDecoderException("readUncompressedRaw: Only 32 bit float point supported");
                    BitBlt(&data[offset.x * sizeof(float) * cpp + y * outPitch], outPitch,
                        input.getData(), inputPitch, w * mRaw.bpp, h - y);
                    return;
                }*/

                if (BitOrder.Jpeg == order)
                {
                    BitPumpMSB bits = new BitPumpMSB(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        bits.checkPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.getBits((uint)bitPerPixel);
                            rawImage.rawData[x + (offset.width * cpp + y * rawImage.dim.width * cpp)] = (ushort)b;
                        }
                        bits.skipBits(skipBits);
                    }
                }
                else if (BitOrder.Jpeg16 == order)
                {
                    BitPumpMSB16 bits = new BitPumpMSB16(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) * cpp + y * outPitch];
                        bits.CheckPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.GetBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.SkipBits(skipBits);
                    }
                }
                else if (BitOrder.Jpeg32 == order)
                {
                    BitPumpMSB32 bits = new BitPumpMSB32(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) * cpp + y * outPitch];
                        bits.checkPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.getBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.skipBits(skipBits);
                    }
                }
                else
                {
                    if (bitPerPixel == 16 && Common.GetHostEndianness() == Endianness.little)
                    {
                        Decode16BitRawUnpacked(input, (uint)w, (uint)h);
                        return;
                    }
                    if (bitPerPixel == 12 && (int)w == inputPitch * 8 / 12 && Common.GetHostEndianness() == Endianness.little)
                    {
                        Decode12BitRaw(input, (uint)w, (uint)h);
                        return;
                    }
                    BitPumpPlain bits = new BitPumpPlain(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) + y * outPitch];
                        bits.checkPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.getBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.skipBits(skipBits);
                    }
                }
            }
        }

        protected void Decode8BitRaw(ref TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (input.GetRemainSize() < w * h)
            {
                if ((UInt32)input.GetRemainSize() > w)
                {
                    h = (uint)(input.GetRemainSize() / w - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Decode8BitRaw: Not enough data to decode a single line. Image file truncated.");
            }

            UInt32 random = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x += 1)
                {
                    rawImage.SetWithLookUp(input.ReadByte(), ref rawImage.rawData, x, ref random);
                    input.Position++;
                }
            }
        }

        protected void Decode12BitRaw(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");
            UInt32 pitch = rawImage.pitch;

            if (input.GetRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.GetRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.GetRemainSize() / (w * 12 / 8) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    UInt32 g3 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                }
            }
        }

        protected void Decode12BitRawWithControl(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            UInt32 pitch = rawImage.pitch;

            // Calulate expected bytes per line.
            UInt32 perline = (w * 12 / 8);
            // Add skips every 10 pixels
            perline += ((w + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.GetRemainSize() < (perline * h))
            {
                if ((UInt32)input.GetRemainSize() > perline)
                {
                    h = (uint)(input.GetRemainSize() / perline - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                {
                    throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }

            UInt32 x;
            for (UInt32 y = 0; y < h; y++)
            {
                for (x = 0; x < w; x += 2)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    UInt32 g3 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        protected void Decode12BitRawBEWithControl(ref TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            UInt32 pitch = rawImage.pitch;

            // Calulate expected bytes per line.
            UInt32 perline = (w * 12 / 8);
            // Add skips every 10 pixels
            perline += ((w + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.GetRemainSize() < (perline * h))
            {
                if ((UInt32)input.GetRemainSize() > perline)
                {
                    h = (uint)(input.GetRemainSize() / perline - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                {
                    throw new IOException("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }

            UInt32 x;
            for (UInt32 y = 0; y < h; y++)
            {
                for (x = 0; x < w; x += 2)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    UInt32 g3 = input.ReadByte();
                    rawImage.rawData[(y * pitch) + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        protected void Decode12BitRawBE(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.GetRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.GetRemainSize() / (w * 12 / 8) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    UInt32 g3 = input.ReadByte();
                    rawImage.rawData[y * pitch + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        protected void Decode12BitRawBEInterlaced(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.GetRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.GetRemainSize() / (w * 12 / 8) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            UInt32 half = (h + 1) >> 1;
            UInt32 y = 0;
            for (UInt32 row = 0; row < h; row++)
            {
                y = row % half * 2 + row / half;
                if (y == 1)
                {
                    // The second field starts at a 2048 byte aligment
                    UInt32 offset = ((half * w * 3 / 2 >> 11) + 1) << 11;
                    if (offset > input.GetRemainSize())
                        throw new IOException("Decode12BitSplitRaw: Trying to jump to invalid offset " + offset);
                    input.Position = offset;
                }
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    UInt32 g3 = input.ReadByte();
                    rawImage.rawData[y * pitch + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        protected void Decode12BitRawBEunpacked(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)(((g1 & 0x0f) << 8) | g2);
                }
            }
        }

        protected void Decode12BitRawBEunpackedLeftAligned(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)(((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        protected void Decode14BitRawBEunpacked(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)(((g1 & 0x3f) << 8) | g2);
                }
            }
        }

        protected void Decode16BitRawUnpacked(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)((g2 << 8) | g1);
                }
            }
        }

        protected void Decode16BitRawBEunpacked(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)((g1 << 8) | g2);
                }
            }
        }

        protected void Decode12BitRawUnpacked(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < w * h * 2)
            {
                if ((UInt32)input.GetRemainSize() > w * 2)
                {
                    h = (uint)(input.GetRemainSize() / (w * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    rawImage.rawData[y * pitch + x] = (ushort)(((g2 << 8) | g1) >> 4);
                }
            }
        }        
    }
}
