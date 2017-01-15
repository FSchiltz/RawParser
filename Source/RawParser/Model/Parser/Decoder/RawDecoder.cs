using RawNet.Decoder.Decompressor;
using RawNet.Format.TIFF;
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
            public uint h = 0;
            public uint offset = 0;
            public uint count = 0;
        }

        /* The decoded image - undefined if image has not or could not be decoded. */
        public RawImage rawImage;

        /* Apply stage 1 DNG opcodes. */
        /* This usually maps out bad pixels, etc */
        protected bool ApplyStage1DngOpcodes { get; set; }

        /* Should Fuji images be rotated? */
        protected bool FujiRotate { get; set; }

        public bool ScaleValue { get; set; } = false;

        /* The Raw input file to be decoded */
        protected TIFFBinaryReader reader;

        /* Hints set for the camera after checkCameraSupported has been called from the implementation*/
        protected Dictionary<string, string> hints = new Dictionary<string, string>();

        protected Stream stream;

        /* Construct decoder instance - FileMap is a filemap of the file to be decoded */
        /* The FileMap is not owned by this class, will not be deleted, and must remain */
        /* valid while this object exists */
        protected RawDecoder(Stream stream)
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

        /* This is faster - at least when compiled on visual studio 32 bits */
        protected int Other_abs(int x)
        {
            int mask = x >> 31;
            return (x + mask) ^ mask;
        }

        /** 
         * Check if the decoder can decode the image from this camera 
         A RawDecoderException will be thrown if the camera isn't supported 
         Unknown cameras does NOT generate any specific feedback 
         This function must be overridden by actual decoders */
        internal void DecodeUncompressed(IFD rawIFD, BitOrder order)
        {
            uint nslices = rawIFD.GetEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = rawIFD.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = rawIFD.GetEntry(TagType.STRIPBYTECOUNTS);
            uint yPerSlice = rawIFD.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
            Int32 width = rawIFD.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            uint height = rawIFD.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            int bitPerPixel = rawIFD.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);

            List<RawSlice> slices = new List<RawSlice>();
            uint offY = 0;

            for (int s = 0; s < nslices; s++)
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

            rawImage.raw.dim.width = width;
            rawImage.raw.dim.height = (int)offY;
            rawImage.whitePoint = (1 << bitPerPixel) - 1;

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
                    ReadUncompressedRaw(input, size, pos, width * bitPerPixel / 8, bitPerPixel, order);
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
        protected unsafe void ReadUncompressedRaw(TIFFBinaryReader input, Point2D size, Point2D offset, int inputPitch, int bitPerPixel, BitOrder order)
        {
            fixed (ushort* d = rawImage.raw.data)
            {
                byte* data = (byte*)d;
                //uint outPitch = rawImage.pitch;
                int w = size.width;
                int h = size.height;
                int cpp = (int)rawImage.cpp;
                int ox = offset.width;
                int oy = offset.height;

                if (input.RemainingSize < (inputPitch * h))
                {
                    if (input.RemainingSize > inputPitch)
                    {
                        h = input.RemainingSize / inputPitch - 1;
                        rawImage.errors.Add("Image truncated (file is too short)");
                    }
                    else
                        throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
                }
                if (bitPerPixel > 16)
                    throw new RawDecoderException("readUncompressedRaw: Unsupported bit depth");

                uint skipBits = (uint)(inputPitch - w * cpp * bitPerPixel / 8);  // Skip per line
                if (oy > rawImage.raw.dim.height)
                    throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
                if (ox + size.width > rawImage.raw.dim.width)
                    throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

                int y = oy;
                h = Math.Min(h + oy, rawImage.raw.dim.height);

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
                    BitPumpMSB bits = new BitPumpMSB(input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        bits.CheckPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.GetBits((uint)bitPerPixel);
                            rawImage.raw.data[x + (offset.width * cpp + y * rawImage.raw.dim.width * cpp)] = (ushort)b;
                        }
                        bits.SkipBits(skipBits);
                    }
                }
                else if (BitOrder.Jpeg16 == order)
                {
                    BitPumpMSB16 bits = new BitPumpMSB16(input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) * cpp + y * rawImage.raw.dim.width];
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
                    BitPumpMSB32 bits = new BitPumpMSB32(input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) * cpp + y * rawImage.raw.dim.width];
                        bits.CheckPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.GetBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.SkipBits(skipBits);
                    }
                }
                else
                {
                    if (bitPerPixel == 16 && Common.GetHostEndianness() == Endianness.Little)
                    {
                        Decode16BitRawUnpacked(input, w, h);
                        return;
                    }
                    if (bitPerPixel == 12 && w == inputPitch * 8 / 12 && Common.GetHostEndianness() == Endianness.Little)
                    {
                        Decode12BitRaw(input, w, h);
                        return;
                    }
                    BitPumpPlain bits = new BitPumpPlain(input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.width * sizeof(UInt16) + y * rawImage.raw.dim.width];
                        bits.CheckPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.GetBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.SkipBits(skipBits);
                    }
                }
            }
        }

        protected void Decode8BitRaw(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (input.RemainingSize < width * height)
            {
                if (input.RemainingSize > width)
                {
                    height = input.RemainingSize / width - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Decode8BitRaw: Not enough data to decode a single line. Image file truncated.");
            }

            uint random = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    rawImage.SetWithLookUp(input.ReadByte(), rawImage.raw.data, x, ref random);
                    input.Position++;
                }
            }
        }

        protected void Decode12BitRaw(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (width < 2) throw new IOException("1 pixel wide raw images are not supported");
            //uint pitch = rawImage.pitch;

            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                if (input.RemainingSize > (width * 12 / 8))
                {
                    height = input.RemainingSize / (width * 12 / 8) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[(y * rawImage.raw.uncroppedDim.width) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.data[(y * rawImage.raw.uncroppedDim.width) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                }
            }
        }

        protected void Decode12BitRawWithControl(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");
            //uint pitch = rawImage.pitch;

            // Calulate expected bytes per line.
            Int32 perline = (width * 12 / 8);
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
                    rawImage.raw.data[(y * rawImage.raw.dim.width) + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                    uint g3 = input.ReadByte();
                    rawImage.raw.data[(y * rawImage.raw.dim.width) + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        protected void Decode12BitRawBEWithControl(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            int pitch = rawImage.raw.dim.width;

            // Calulate expected bytes per line.
            Int32 perline = (width * 12 / 8);
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
                    rawImage.raw.data[(y * pitch) + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.data[(y * pitch) + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                    if ((x % 10) == 8) input.Position++;
                }
            }
        }

        protected void Decode12BitRawBE(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            int pitch = rawImage.raw.dim.width;
            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                if (input.RemainingSize > (width * 12 / 8))
                {
                    height = (input.RemainingSize / (width * 12 / 8) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * pitch + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.data[y * pitch + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        protected void Decode12BitRawBEInterlaced(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            if (width < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");

            int pitch = rawImage.raw.dim.width;
            if (input.RemainingSize < ((width * 12 / 8) * height))
            {
                if (input.RemainingSize > (width * 12 / 8))
                {
                    height = input.RemainingSize / (width * 12 / 8) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            int half = (height + 1) >> 1;
            int y = 0;
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
                    rawImage.raw.data[y * pitch + x] = (ushort)((g1 << 4) | (g2 >> 4));
                    uint g3 = input.ReadByte();
                    rawImage.raw.data[y * pitch + x + 1] = (ushort)(((g2 & 0x0f) << 8) | g3);
                }
            }
        }

        protected void Decode12BitRawBEunpacked(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            int pitch = rawImage.raw.dim.width;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = input.RemainingSize / (width * 2) - 1;
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * pitch + x] = (ushort)(((g1 & 0x0f) << 8) | g2);
                }
            }
        }

        protected void Decode12BitRawBEunpackedLeftAligned(TIFFBinaryReader input, Int32 width, Int32 height)
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
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * rawImage.raw.dim.width + x] = (ushort)(((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        protected void Decode14BitRawBEunpacked(TIFFBinaryReader input, Int32 width, Int32 height)
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
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * rawImage.raw.dim.width + x] = (ushort)(((g1 & 0x3f) << 8) | g2);
                }
            }
        }

        protected void Decode16BitRawUnpacked(TIFFBinaryReader input, Int32 width, Int32 height)
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
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * rawImage.raw.dim.width + x] = (ushort)((g2 << 8) | g1);
                }
            }
        }

        protected void Decode16BitRawBEunpacked(TIFFBinaryReader input, Int32 width, Int32 height)
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
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * rawImage.raw.dim.width + x] = (ushort)((g1 << 8) | g2);
                }
            }
        }

        protected void Decode12BitRawUnpacked(TIFFBinaryReader input, Int32 width, Int32 height)
        {
            int pitch = rawImage.raw.dim.width;
            if (input.RemainingSize < width * height * 2)
            {
                if (input.RemainingSize > width * 2)
                {
                    height = (input.RemainingSize / (width * 2) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x += 1)
                {
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    rawImage.raw.data[y * pitch + x] = (ushort)(((g2 << 8) | g1) >> 4);
                }
            }
        }
    }
}
