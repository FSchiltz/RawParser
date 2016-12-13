using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /* Remember this is automatically refcounted, so a reference is retained until this class is destroyed */
        public RawImage rawImage;

        /* Set how to handle bad pixels. */
        /* If you disable this parameter, no bad pixel interpolation will be done */
        protected bool interpolateBadPixels { get; set; }

        /* Apply stage 1 DNG opcodes. */
        /* This usually maps out bad pixels, etc */
        protected bool applyStage1DngOpcodes { get; set; }

        /* Apply crop - if false uncropped image is delivered */
        protected bool applyCrop { get; set; }

        /* Should Fuji images be rotated? */
        protected bool fujiRotate { get; set; }


        /* Retrieve the main RAW chunk */
        /* Returns null if unknown */
        public byte[] getCompressedData()
        {
            return null;
        }

        /* The Raw input file to be decoded */
        protected TIFFBinaryReader reader;

        /* Decoder version - defaults to 0, but can be overridden by decoders */
        /* This can be used to avoid newer version of an xml file to indicate that a file */
        /* can be decoded, when a specific version of the code is needed */
        /* Higher number in camera xml file: Files for this camera will not be decoded */
        /* Higher number in code than xml: Image will be decoded. */
        protected int decoderVersion;

        /* Hints set for the camera after checkCameraSupported has been called from the implementation*/
        protected Dictionary<string, string> hints = new Dictionary<string, string>();

        protected CameraMetaData metaData;

        /* Construct decoder instance - FileMap is a filemap of the file to be decoded */
        /* The FileMap is not owned by this class, will not be deleted, and must remain */
        /* valid while this object exists */
        public RawDecoder(CameraMetaData metaData)
        {
            rawImage = new RawImage();
            decoderVersion = 0;
            interpolateBadPixels = false;
            applyStage1DngOpcodes = true;
            applyCrop = true;
            fujiRotate = true;
            this.metaData = metaData;
        }

        public RawImage DecodeRaw()
        {
            try
            {
                RawImage raw = decodeRawInternal();
                hints.TryGetValue("pixel_aspect_ratio", out string pixelRatio);
                if (pixelRatio != null)
                {
                    raw.metadata.pixelAspectRatio = Double.Parse(pixelRatio);
                }
                //if (!uncorrectedRawValues) mRaw.scaleValues();
                /*
                if (interpolateBadPixels)
                    raw-fixBadPixels();*/
                return raw;
            }
            catch (TiffParserException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (FileIOException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (IOException e)
            {
                throw new RawDecoderException(e.Message);
            }
        }

        /*
         * return a byte[] containing an JPEG image or null if the file doesn't have a thumbnail
         */
        public Thumbnail decodeThumb()
        {
            try
            {
                return decodeThumbInternal();
            }
            catch (TiffParserException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (FileIOException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (IOException e)
            {
                throw new RawDecoderException(e.Message);
            }
        }

        /* Check if the decoder can decode the image from this camera */
        /* A RawDecoderException will be thrown if the camera isn't supported */
        /* Unknown cameras does NOT generate any specific feedback */
        /* This function must be overridden by actual decoders */
        internal void decodeUncompressed(ref IFD rawIFD, BitOrder order)
        {
            UInt32 nslices = rawIFD.getEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = rawIFD.getEntry(TagType.STRIPOFFSETS);
            Tag counts = rawIFD.getEntry(TagType.STRIPBYTECOUNTS);
            UInt32 yPerSlice = rawIFD.getEntry(TagType.ROWSPERSTRIP).getUInt();
            Int32 width = rawIFD.getEntry(TagType.IMAGEWIDTH).getInt();
            UInt32 height = rawIFD.getEntry(TagType.IMAGELENGTH).getUInt();
            int bitPerPixel = rawIFD.getEntry(TagType.BITSPERSAMPLE).getInt();

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

                if (reader.isValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            if (0 == slices.Count)
                throw new RawDecoderException("RAW Decoder: No valid slices found. File probably truncated.");

            rawImage.dim.x = width;
            rawImage.dim.y = (int)offY;
            rawImage.whitePoint = (uint)(1 << bitPerPixel) - 1;

            offY = 0;
            for (int i = 0; i < slices.Count; i++)
            {
                RawSlice slice = slices[i];
                var stream = reader.BaseStream;
                TIFFBinaryReader input;
                if (reader is TIFFBinaryReaderRE) input = new TIFFBinaryReaderRE(reader.BaseStream, slice.offset, slice.count);
                else input = new TIFFBinaryReader(reader.BaseStream, slice.offset, slice.count);
                Point2D size = new Point2D(width, (int)slice.h);
                Point2D pos = new Point2D(0, (int)offY);
                bitPerPixel = (int)(slice.count * 8u / (slice.h * width));
                try
                {
                    readUncompressedRaw(ref input, size, pos, width * bitPerPixel / 8, bitPerPixel, order);
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

        /* Attempt to decode the image */
        /* A RawDecoderException will be thrown if the image cannot be decoded
        protected void readUncompressedRaw(ref TIFFBinaryReader input, iPoint2D size, iPoint2D offset, int inputPitch, int bitPerPixel, BitOrder order)
        {
            UInt32 outPitch = mRaw.pitch;
            uint w = (uint)size.x;
            uint h = (uint)size.y;
            UInt32 cpp = mRaw.cpp;
            UInt64 ox = (ulong)offset.x;
            UInt64 oy = (ulong)offset.y;
            if (this.mRaw.rawData == null)
            {
                mRaw.rawData = new ushort[w * h * cpp];
            }
            if (input.getRemainSize() < (inputPitch * (int)h))
            {
                if (input.getRemainSize() > inputPitch)
                {
                    h = (uint)(input.getRemainSize() / inputPitch - 1);
                    mRaw.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            if (bitPerPixel > 16)
                throw new RawDecoderException("readUncompressedRaw: Unsupported bit depth");

            UInt32 skipBits = (uint)(inputPitch - (int)w * cpp * bitPerPixel / 8);  // Skip per line
            if (oy > (ulong)mRaw.dim.y)
                throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
            if (ox + (ulong)size.x > (ulong)mRaw.dim.x)
                throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

            UInt64 y = oy;
            h = (uint)Math.Min(h + (uint)oy, mRaw.dim.y);
            /*
            if (mRaw.getDataType() == RawImageType.TYPE_FLOAT32)
            {
                if (bitPerPixel != 32)
                    throw new RawDecoderException("readUncompressedRaw: Only 32 bit float point supported");
                BitBlt(&data[offset.x * sizeof(float) * cpp + y * outPitch], outPitch,
                    input.getData(), inputPitch, w * mRaw.bpp, h - y);
                return;
            }

            if (BitOrder.Jpeg == order)
            {
                BitPumpMSB bits = new BitPumpMSB(ref input);
                w *= cpp;
                for (; y < h; y++)
                {
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits((uint)bitPerPixel);
                        mRaw.rawData[(((int)(offset.x * sizeof(UInt16) * cpp) + (int)y * (int)outPitch)) + x] = (ushort)b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else if (BitOrder.Jpeg16 == order)
            {
                BitPumpMSB16 bits = new BitPumpMSB16(input);
                w *= cpp;
                for (; y < h; y++)
                {
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits((uint)bitPerPixel);
                        mRaw.rawData[(offset.x * sizeof(ushort) * (int)cpp + (int)y * (int)outPitch) + x] = (ushort)b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else if (BitOrder.Jpeg32 == order)
            {
                BitPumpMSB32 bits = new BitPumpMSB32(input);
                w *= cpp;
                for (; y < h; y++)
                {
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits((uint)bitPerPixel);
                        mRaw.rawData[(offset.x * sizeof(ushort) * (int)cpp + (int)y * (int)outPitch) + x] = (ushort)b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else
            {
                if (bitPerPixel == 16)
                {
                    Decode16BitRawUnpacked(input, w, h);
                    return;
                }
                if (bitPerPixel == 12 && (int)w == inputPitch * 8 / 12)
                {
                    Decode12BitRaw(input, w, h);
                    return;
                }
                BitPumpPlain bits = new BitPumpPlain(input);
                w *= cpp;
                for (; y < h; y++)
                {
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits((uint)bitPerPixel);
                        mRaw.rawData[(offset.x * sizeof(ushort) + (int)y * (int)outPitch) + x] = (ushort)b;
                    }
                    bits.skipBits(skipBits);
                }
            }
        }
        */

        protected unsafe void readUncompressedRaw(ref TIFFBinaryReader input, Point2D size, Point2D offset, int inputPitch, int bitPerPixel, BitOrder order)
        {
            fixed (ushort* d = rawImage.rawData)
            {
                byte* data = (byte*)d;
                uint outPitch = rawImage.pitch;
                int w = size.x;
                int h = size.y;
                uint cpp = rawImage.cpp;
                int ox = offset.x;
                int oy = offset.y;

                if (input.getRemainSize() < (inputPitch * h))
                {
                    if ((int)input.getRemainSize() > inputPitch)
                    {
                        h = input.getRemainSize() / inputPitch - 1;
                        rawImage.errors.Add("Image truncated (file is too short)");
                    }
                    else
                        throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
                }
                if (bitPerPixel > 16)
                    throw new RawDecoderException("readUncompressedRaw: Unsupported bit depth");

                uint skipBits = (uint)(inputPitch - w * cpp * bitPerPixel / 8);  // Skip per line
                if (oy > rawImage.dim.y)
                    throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
                if (ox + size.x > rawImage.dim.x)
                    throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

                int y = oy;
                h = (int)Math.Min(h + oy, (uint)rawImage.dim.y);

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
                            rawImage.rawData[x + (offset.x * cpp + y * rawImage.dim.x * cpp)] = (ushort)b;
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
                        UInt16* dest = (UInt16*)&data[offset.x * sizeof(UInt16) * cpp + y * outPitch];
                        bits.checkPos();
                        for (uint x = 0; x < w; x++)
                        {
                            uint b = bits.getBits((uint)bitPerPixel);
                            dest[x] = (ushort)b;
                        }
                        bits.skipBits(skipBits);
                    }
                }
                else if (BitOrder.Jpeg32 == order)
                {
                    BitPumpMSB32 bits = new BitPumpMSB32(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.x * sizeof(UInt16) * cpp + y * outPitch];
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
                    if (bitPerPixel == 16 && Common.getHostEndianness() == Endianness.little)
                    {
                        Decode16BitRawUnpacked(input, (uint)w, (uint)h);
                        return;
                    }
                    if (bitPerPixel == 12 && (int)w == inputPitch * 8 / 12 && Common.getHostEndianness() == Endianness.little)
                    {
                        Decode12BitRaw(input, (uint)w, (uint)h);
                        return;
                    }
                    BitPumpPlain bits = new BitPumpPlain(ref input);
                    w *= (int)cpp;
                    for (; y < h; y++)
                    {
                        UInt16* dest = (UInt16*)&data[offset.x * sizeof(UInt16) + y * outPitch];
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
            ushort[] data = rawImage.rawData;
            UInt32 pitch = rawImage.pitch;
            if (input.getRemainSize() < w * h)
            {
                if ((UInt32)input.getRemainSize() > w)
                {
                    h = (uint)(input.getRemainSize() / w - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Decode8BitRaw: Not enough data to decode a single line. Image file truncated.");
            }

            UInt32 random = 0;
            for (UInt32 y = 0; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x += 1)
                {
                    rawImage.setWithLookUp(input.ReadByte(), ref rawImage.rawData, x, ref random);
                    input.Position++;
                }
            }
        }

        protected void Decode12BitRaw(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 2) throw new IOException("Are you mad? 1 pixel wide raw images are no fun");
            UInt32 pitch = rawImage.pitch;

            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.getRemainSize() / (w * 12 / 8) - 1);
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
            if (input.getRemainSize() < (perline * h))
            {
                if ((UInt32)input.getRemainSize() > perline)
                {
                    h = (uint)(input.getRemainSize() / perline - 1);
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
            if (input.getRemainSize() < (perline * h))
            {
                if ((UInt32)input.getRemainSize() > perline)
                {
                    h = (uint)(input.getRemainSize() / perline - 1);
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
            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.getRemainSize() / (w * 12 / 8) - 1);
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
            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = (uint)(input.getRemainSize() / (w * 12 / 8) - 1);
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
                    if (offset > input.getRemainSize())
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = (uint)(input.getRemainSize() / (w * 2) - 1);
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

        protected bool checkCameraSupported(CameraMetaData meta, string make, string model, string mode)
        {
            make = make.Trim();
            model = model.Trim();
            rawImage.metadata.make = make;
            rawImage.metadata.model = model;
            Camera cam = meta.getCamera(make, model, mode);
            if (cam == null)
            {
                if (mode.Length == 0)
                    Debug.WriteLine("Unable to find camera in database: " + make + " " + model + " " + mode);

                Debug.WriteLine("Camera " + make + " " + model + ", mode " + mode + " not supported, and not allowed to guess. Sorry.");

                // Assume the camera can be decoded, but return false, so decoders can see that we are unsure.
                return false;
            }

            if (!cam.supported)
                throw new RawDecoderException("Camera not supported (explicit). Sorry.");

            if (cam.decoderVersion > decoderVersion)
                throw new RawDecoderException("Camera not supported in this version. Update RawSpeed for support.");

            hints = cam.hints;
            return true;
        }

        protected void setMetaData(CameraMetaData meta, string make, string model, string mode, int iso_speed)
        {
            rawImage.metadata.isoSpeed = iso_speed;
            make = make.Trim();
            model = model.Trim();
            Camera cam = meta.getCamera(make, model, mode);
            if (cam == null)
            {
                Debug.WriteLine("ISO:" + iso_speed);
                Debug.WriteLine("Unable to find camera in database: " + make + " " + model + " " + mode + "\nPlease upload file to ftp.rawstudio.org, thanks!");
                return;
            }

            rawImage.cfa = cam.cfa;
            rawImage.metadata.canonical_make = cam.canonical_make;
            rawImage.metadata.canonical_model = cam.canonical_model;
            rawImage.metadata.canonical_alias = cam.canonical_alias;
            rawImage.metadata.canonical_id = cam.canonical_id;
            rawImage.metadata.make = make;
            rawImage.metadata.model = model;
            rawImage.metadata.mode = mode;

            if (applyCrop)
            {
                Point2D new_size = cam.cropSize;

                // If crop size is negative, use relative cropping
                if (new_size.x <= 0)
                    new_size.x = rawImage.dim.x - cam.cropPos.x + new_size.x;

                if (new_size.y <= 0)
                    new_size.y = rawImage.dim.y - cam.cropPos.y + new_size.y;

                rawImage.subFrame(new Rectangle2D(cam.cropPos, new_size));


                // Shift CFA to match crop
                rawImage.UncroppedCfa = new ColorFilterArray(rawImage.cfa);
                if ((cam.cropPos.x & 1) != 0)
                    rawImage.cfa.shiftLeft(0);
                if ((cam.cropPos.y & 1) != 0)
                    rawImage.cfa.shiftDown(0);
            }

            CameraSensorInfo sensor = cam.getSensorInfo(iso_speed);
            rawImage.blackLevel = sensor.blackLevel;
            rawImage.whitePoint = (uint)sensor.whiteLevel;
            rawImage.blackAreas = cam.blackAreas;
            if (rawImage.blackAreas.Count == 0 && sensor.mBlackLevelSeparate.Count != 0)
            {
                if (rawImage.isCFA && rawImage.cfa.size.area() <= sensor.mBlackLevelSeparate.Count)
                {
                    for (UInt32 i = 0; i < rawImage.cfa.size.area(); i++)
                    {
                        rawImage.blackLevelSeparate[i] = sensor.mBlackLevelSeparate[(int)i];
                    }
                }
                else if (!rawImage.isCFA && rawImage.cpp <= sensor.mBlackLevelSeparate.Count)
                {
                    for (UInt32 i = 0; i < rawImage.cpp; i++)
                    {
                        rawImage.blackLevelSeparate[i] = sensor.mBlackLevelSeparate[(int)i];
                    }
                }
            }

            // Allow overriding individual blacklevels. Values are in CFA order
            // (the same order as the in the CFA tag)
            // A hint could be:
            // <Hint name="override_cfa_black" value="10,20,30,20"/>
            cam.hints.TryGetValue("override_cfa_black", out string value);
            if (value != null)
            {
                string rgb = value;
                var v = rgb.Split(',');
                if (v.Length != 4)
                {
                    rawImage.errors.Add("Expected 4 values '10,20,30,20' as values for override_cfa_black hint.");
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        rawImage.blackLevelSeparate[i] = Int32.Parse(v[i]);
                    }
                }
            }
        }

        public void decodeMetaData()
        {
            try
            {
                decodeMetaDataInternal();
            }
            catch (TiffParserException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (FileIOException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (IOException e)
            {
                throw new RawDecoderException(e.Message);
            }
        }

        public void checkSupport()
        {
            try
            {
                checkSupportInternal();
            }
            catch (TiffParserException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (FileIOException e)
            {
                throw new RawDecoderException(e.Message);
            }
            catch (IOException e)
            {
                throw new RawDecoderException(e.Message);
            }
        }
        /* Attempt to decode the image */
        /* A RawDecoderException will be thrown if the image cannot be decoded, */
        /* and there will not be any data in the mRaw image. */
        /* This function must be overridden by actual decoders. */
        protected abstract RawImage decodeRawInternal();
        protected virtual Thumbnail decodeThumbInternal() { return null; }
        protected abstract void decodeMetaDataInternal();
        protected abstract void checkSupportInternal();
    }
}
