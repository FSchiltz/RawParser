using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSpeed
{
    //TODO fix comment from original
    public class RawDecoder
    {

        /* Class with information delivered to decodeThreaded() */
        public class RawDecoderThread
        {
            public RawDecoderThread()
            {
                error = (char)0;
                //= -1 in original check why
                taskNo = 0;
            }
            UInt32 start_y;
            UInt32 end_y;
            char error;
            Task threadid;
            RawDecoder parent;
            UInt32 taskNo;
        }
        /* The decoded image - undefined if image has not or could not be decoded. */
        /* Remember this is automatically refcounted, so a reference is retained until this class is destroyed */
        public RawImage mRaw;

        /* You can set this if you do not want Rawspeed to attempt to decode images, */
        /* where it does not have reliable information about CFA, cropping, black and white point */
        /* It is pretty safe to leave this disabled (default behaviour), but if you do not want to */
        /* support unknown cameras, you can enable this */
        /* DNGs are always attempted to be decoded, so this variable has no effect on DNGs */
        public bool failOnUnknown;

        /* Set how to handle bad pixels. */
        /* If you disable this parameter, no bad pixel interpolation will be done */
        bool interpolateBadPixels;

        /* Apply stage 1 DNG opcodes. */
        /* This usually maps out bad pixels, etc */
        bool applyStage1DngOpcodes;

        /* Apply crop - if false uncropped image is delivered */
        bool applyCrop;

        /* This will skip all corrections, and deliver the raw data */
        /* This will skip any compression curves or other things that */
        /* is needed to get the correct values */
        /* Only enable if you are sure that is what you want */
        bool uncorrectedRawValues;

        /* Should Fuji images be rotated? */
        bool fujiRotate;

        /* Vector of objects that will be destroyed alongside the decoder */
        List<FileMap> ownedObjects;

        /* Retrieve the main RAW chunk */
        /* Returns null if unknown */
        FileMap getCompressedData()
        {
            return null;
        }
        /* The Raw input file to be decoded */
        FileMap mFile;

        /* Decoder version - defaults to 0, but can be overridden by decoders */
        /* This can be used to avoid newer version of an xml file to indicate that a file */
        /* can be decoded, when a specific version of the code is needed */
        /* Higher number in camera xml file: Files for this camera will not be decoded */
        /* Higher number in code than xml: Image will be decoded. */
        int decoderVersion;

        /* Hints set for the camera after checkCameraSupported has been called from the implementation*/
        Dictionary<string, string> hints;


        class RawSlice
        {
            public RawSlice() { }
            UInt32 h = 0;
            UInt32 offset = 0;
            UInt32 count = 0;
        }

        /* Construct decoder instance - FileMap is a filemap of the file to be decoded */
        /* The FileMap is not owned by this class, will not be deleted, and must remain */
        /* valid while this object exists */
        RawDecoder(ref FileMap file)
        {
            mRaw = RawImage.create();
            mFile = file;
            decoderVersion = 0;
            failOnUnknown = false;
            interpolateBadPixels = true;
            applyStage1DngOpcodes = true;
            applyCrop = true;
            uncorrectedRawValues = false;
            fujiRotate = true;
        }

        /* Check if the decoder can decode the image from this camera */
        /* A RawDecoderException will be thrown if the camera isn't supported */
        /* Unknown cameras does NOT generate any specific feedback */
        /* This function must be overridden by actual decoders */
        void decodeUncompressed(ref TiffIFD rawIFD, BitOrder order)
        {
            UInt32 nslices = rawIFD.getEntry(STRIPOFFSETS).count;
            TiffEntry* offsets = rawIFD.getEntry(STRIPOFFSETS);
            TiffEntry* counts = rawIFD.getEntry(STRIPBYTECOUNTS);
            UInt32 yPerSlice = rawIFD.getEntry(ROWSPERSTRIP).getInt();
            UInt32 width = rawIFD.getEntry(IMAGEWIDTH).getInt();
            UInt32 height = rawIFD.getEntry(IMAGELENGTH).getInt();
            UInt32 bitPerPixel = rawIFD.getEntry(BITSPERSAMPLE).getInt();

            vector<RawSlice> slices;
            UInt32 offY = 0;

            for (UInt32 s = 0; s < nslices; s++)
            {
                RawSlice slice;
                slice.offset = offsets.getInt(s);
                slice.count = counts.getInt(s);
                if (offY + yPerSlice > height)
                    slice.h = height - offY;
                else
                    slice.h = yPerSlice;

                offY += yPerSlice;

                if (mFile.isValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.push_back(slice);
            }

            if (0 == slices.size())
                ThrowRDE("RAW Decoder: No valid slices found. File probably truncated.");

            mRaw.dim = iPoint2D(width, offY);
            mRaw.createData();
            mRaw.whitePoint = (1 << bitPerPixel) - 1;

            offY = 0;
            for (UInt32 i = 0; i < slices.size(); i++)
            {
                RawSlice slice = slices[i];
                ByteStream in(mFile, slice.offset, slice.count);
                iPoint2D size(width, slice.h);
                iPoint2D pos(0, offY);
            bitPerPixel = (int)((UInt64)((UInt64)slice.count * 8u) / (slice.h * width));
            try
            {
                readUncompressedRaw(in, size, pos, width * bitPerPixel / 8, bitPerPixel, order);
            }
            catch (RawDecoderException &e) {
                if (i > 0)
                    mRaw.setError(e.what());
                else
                    throw;
            } catch (IOException &e) {
                if (i > 0)
                    mRaw.setError(e.what());
                else
                    ThrowRDE("RAW decoder: IO error occurred in first slice, unable to decode more. Error is: %s", e.what());
            }
            offY += slice.h;
            }
        }


        /* Attempt to decode the image */
        /* A RawDecoderException will be thrown if the image cannot be decoded, */
        /* and there will not be any data in the mRaw image. */
        void readUncompressedRaw(ByteStream &input, iPoint2D& size, iPoint2D& offset, int inputPitch, int bitPerPixel, BitOrder order)
        {
            byte8* data = mRaw.getData();
            UInt32 outPitch = mRaw.pitch;
            UInt64 w = size.x;
            UInt64 h = size.y;
            UInt32 cpp = mRaw.getCpp();
            UInt64 ox = offset.x;
            UInt64 oy = offset.y;

            if (input.getRemainSize() < (inputPitch * h))
            {
                if ((int)input.getRemainSize() > inputPitch)
                {
                    h = input.getRemainSize() / inputPitch - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            if (bitPerPixel > 16 && mRaw.getDataType() == TYPE_USHORT16)
                ThrowRDE("readUncompressedRaw: Unsupported bit depth");

            UInt32 skipBits = inputPitch - w * cpp * bitPerPixel / 8;  // Skip per line
            if (oy > (UInt64)mRaw.dim.y)
                ThrowRDE("readUncompressedRaw: Invalid y offset");
            if (ox + size.x > (UInt64)mRaw.dim.x)
                ThrowRDE("readUncompressedRaw: Invalid x offset");

            UInt64 y = oy;
            h = Math.Min(h + oy, (UInt32)mRaw.dim.y);

            if (mRaw.getDataType() == TYPE_FLOAT32)
            {
                if (bitPerPixel != 32)
                    ThrowRDE("readUncompressedRaw: Only 32 bit float point supported");
                BitBlt(&data[offset.x * sizeof(float) * cpp + y * outPitch], outPitch,
                    input.getData(), inputPitch, w * mRaw.getBpp(), h - y);
                return;
            }

            if (BitOrder_Jpeg == order)
            {
                BitPumpMSB bits(&input);
                w *= cpp;
                for (; y < h; y++)
                {
                    ushort16* dest = (ushort16*)&data[offset.x * sizeof(ushort16) * cpp + y * outPitch];
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits(bitPerPixel);
                        dest[x] = b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else if (BitOrder_Jpeg16 == order)
            {
                BitPumpMSB16 bits(&input);
                w *= cpp;
                for (; y < h; y++)
                {
                    ushort16* dest = (ushort16*)&data[offset.x * sizeof(ushort16) * cpp + y * outPitch];
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits(bitPerPixel);
                        dest[x] = b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else if (BitOrder_Jpeg32 == order)
            {
                BitPumpMSB32 bits(&input);
                w *= cpp;
                for (; y < h; y++)
                {
                    ushort16* dest = (ushort16*)&data[offset.x * sizeof(ushort16) * cpp + y * outPitch];
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits(bitPerPixel);
                        dest[x] = b;
                    }
                    bits.skipBits(skipBits);
                }
            }
            else
            {
                if (bitPerPixel == 16 && getHostEndianness() == little)
                {
                    BitBlt(&data[offset.x * sizeof(ushort16) * cpp + y * outPitch], outPitch,
                           input.getData(), inputPitch, w * mRaw.getBpp(), h - y);
                    return;
                }
                if (bitPerPixel == 12 && (int)w == inputPitch * 8 / 12 && getHostEndianness() == little)
                {
                    Decode12BitRaw(input, w, h);
                    return;
                }
                BitPumpPlain bits(&input);
                w *= cpp;
                for (; y < h; y++)
                {
                    ushort16* dest = (ushort16*)&data[offset.x * sizeof(ushort16) + y * outPitch];
                    bits.checkPos();
                    for (UInt32 x = 0; x < w; x++)
                    {
                        UInt32 b = bits.getBits(bitPerPixel);
                        dest[x] = b;
                    }
                    bits.skipBits(skipBits);
                }
            }
        }

        void Decode8BitRaw(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h)
            {
                if ((UInt32)input.getRemainSize() > w)
                {
                    h = input.getRemainSize() / w - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("Decode8BitRaw: Not enough data to decode a single line. Image file truncated.");
            }

            UInt32 random = 0;
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    if (uncorrectedRawValues)
                        dest[x] = *in++;
      else
        mRaw.setWithLookUp(*in++, (byte8*)&dest[x], &random);
                }
            }
        }

        void Decode12BitRaw(ByteStream &input, UInt32 w, UInt32 h)
        {
            if (w < 2) ThrowIOE("Are you mad? 1 pixel wide raw images are no fun");

            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();

            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = input.getRemainSize() / (w * 12 / 8) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = g1 | ((g2 & 0xf) << 8);
                    UInt32 g3 = *in++;
                    dest[x + 1] = (g2 >> 4) | (g3 << 4);
                }
            }
        }

        void Decode12BitRawWithControl(ByteStream &input, UInt32 w, UInt32 h)
        {
            if (w < 2) ThrowIOE("Are you mad? 1 pixel wide raw images are no fun");

            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();

            // Calulate expected bytes per line.
            UInt32 perline = (w * 12 / 8);
            // Add skips every 10 pixels
            perline += ((w + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.getRemainSize() < (perline * h))
            {
                if ((UInt32)input.getRemainSize() > perline)
                {
                    h = input.getRemainSize() / perline - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                {
                    ThrowIOE("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }

            UInt32 x;
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (x = 0; x < w; x += 2)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = g1 | ((g2 & 0xf) << 8);
                    UInt32 g3 = *in++;
                    dest[x + 1] = (g2 >> 4) | (g3 << 4);
                    if ((x % 10) == 8)
        in++;
                }
            }
        }

        void Decode12BitRawBEWithControl(ByteStream &input, UInt32 w, UInt32 h)
        {
            if (w < 2) ThrowIOE("Are you mad? 1 pixel wide raw images are no fun");

            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();

            // Calulate expected bytes per line.
            UInt32 perline = (w * 12 / 8);
            // Add skips every 10 pixels
            perline += ((w + 2) / 10);

            // If file is too short, only decode as many lines as we have
            if (input.getRemainSize() < (perline * h))
            {
                if ((UInt32)input.getRemainSize() > perline)
                {
                    h = input.getRemainSize() / perline - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                {
                    ThrowIOE("Decode12BitRawBEWithControl: Not enough data to decode a single line. Image file truncated.");
                }
            }

            UInt32 x;
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (x = 0; x < w; x += 2)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (g1 << 4) | (g2 >> 4);
                    UInt32 g3 = *in++;
                    dest[x + 1] = ((g2 & 0x0f) << 8) | g3;
                    if ((x % 10) == 8)
        in++;
                }
            }
        }

        void Decode12BitRawBE(ByteStream &input, UInt32 w, UInt32 h)
        {
            if (w < 2) ThrowIOE("Are you mad? 1 pixel wide raw images are no fun");

            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = input.getRemainSize() / (w * 12 / 8) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (g1 << 4) | (g2 >> 4);
                    UInt32 g3 = *in++;
                    dest[x + 1] = ((g2 & 0x0f) << 8) | g3;
                }
            }
        }

        void Decode12BitRawBEInterlaced(ByteStream &input, UInt32 w, UInt32 h)
        {
            if (w < 2) ThrowIOE("Are you mad? 1 pixel wide raw images are no fun");

            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < ((w * 12 / 8) * h))
            {
                if ((UInt32)input.getRemainSize() > (w * 12 / 8))
                {
                    h = input.getRemainSize() / (w * 12 / 8) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            UInt32 half = (h + 1) >> 1;
            UInt32 y = 0;
            for (UInt32 row = 0; row < h; row++)
            {
                y = row % half * 2 + row / half;
                ushort16* dest = (ushort16*)&data[y * pitch];
                if (y == 1)
                {
                    // The second field starts at a 2048 byte aligment
                    UInt32 offset = ((half * w * 3 / 2 >> 11) + 1) << 11;
                    if (offset > input.getRemainSize())
                        ThrowIOE("Decode12BitSplitRaw: Trying to jump to invalid offset %d", offset);
      in = input.getData() + offset;
                }
                for (UInt32 x = 0; x < w; x += 2)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (g1 << 4) | (g2 >> 4);
                    UInt32 g3 = *in++;
                    dest[x + 1] = ((g2 & 0x0f) << 8) | g3;
                }
            }
        }

        void Decode12BitRawBEunpacked(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = ((g1 & 0x0f) << 8) | g2;
                }
            }
        }

        void Decode12BitRawBEunpackedLeftAligned(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (((g1 << 8) | (g2 & 0xf0)) >> 4);
                }
            }
        }

        void Decode14BitRawBEunpacked(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = ((g1 & 0x3f) << 8) | g2;
                }
            }
        }

        void Decode16BitRawUnpacked(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (g2 << 8) | g1;
                }
            }
        }

        void Decode16BitRawBEunpacked(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = (g1 << 8) | g2;
                }
            }
        }

        void Decode12BitRawUnpacked(ByteStream &input, UInt32 w, UInt32 h)
        {
            byte8* data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            byte8*in = input.getData();
            if (input.getRemainSize() < w * h * 2)
            {
                if ((UInt32)input.getRemainSize() > w * 2)
                {
                    h = input.getRemainSize() / (w * 2) - 1;
                    mRaw.setError("Image truncated (file is too short)");
                }
                else
                    ThrowIOE("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }
            for (UInt32 y = 0; y < h; y++)
            {
                ushort16* dest = (ushort16*)&data[y * pitch];
                for (UInt32 x = 0; x < w; x += 1)
                {
                    UInt32 g1 = *in++;
                    UInt32 g2 = *in++;
                    dest[x] = ((g2 << 8) | g1) >> 4;
                }
            }
        }

        bool checkCameraSupported(CameraMetaData* meta, string make, string model, string mode)
        {
            TrimSpaces(make);
            TrimSpaces(model);
            mRaw.metadata.make = make;
            mRaw.metadata.model = model;
            Camera* cam = meta.getCamera(make, model, mode);
            if (!cam)
            {
                if (mode.length() == 0)
                    writeLog(DEBUG_PRIO_WARNING, "Unable to find camera in database: %s %s %s\n", make.c_str(), model.c_str(), mode.c_str());

                if (failOnUnknown)
                    ThrowRDE("Camera '%s' '%s', mode '%s' not supported, and not allowed to guess. Sorry.", make.c_str(), model.c_str(), mode.c_str());

                // Assume the camera can be decoded, but return false, so decoders can see that we are unsure.
                return false;
            }

            if (!cam.supported)
                ThrowRDE("Camera not supported (explicit). Sorry.");

            if (cam.decoderVersion > decoderVersion)
                ThrowRDE("Camera not supported in this version. Update RawSpeed for support.");

            hints = cam.hints;
            return true;
        }

        void setMetaData(CameraMetaData* meta, string make, string model, string mode, int iso_speed)
        {
            mRaw.metadata.isoSpeed = iso_speed;
            TrimSpaces(make);
            TrimSpaces(model);
            Camera* cam = meta.getCamera(make, model, mode);
            if (!cam)
            {
                writeLog(DEBUG_PRIO_INFO, "ISO:%d\n", iso_speed);
                writeLog(DEBUG_PRIO_WARNING, "Unable to find camera in database: %s %s %s\nPlease upload file to ftp.rawstudio.org, thanks!\n", make.c_str(), model.c_str(), mode.c_str());
                return;
            }

            mRaw.cfa = cam.cfa;
            mRaw.metadata.canonical_make = cam.canonical_make;
            mRaw.metadata.canonical_model = cam.canonical_model;
            mRaw.metadata.canonical_alias = cam.canonical_alias;
            mRaw.metadata.canonical_id = cam.canonical_id;
            mRaw.metadata.make = make;
            mRaw.metadata.model = model;
            mRaw.metadata.mode = mode;

            if (applyCrop)
            {
                iPoint2D new_size = cam.cropSize;

                // If crop size is negative, use relative cropping
                if (new_size.x <= 0)
                    new_size.x = mRaw.dim.x - cam.cropPos.x + new_size.x;

                if (new_size.y <= 0)
                    new_size.y = mRaw.dim.y - cam.cropPos.y + new_size.y;

                mRaw.subFrame(iRectangle2D(cam.cropPos, new_size));

                // Shift CFA to match crop
                if (cam.cropPos.x & 1)
                    mRaw.cfa.shiftLeft();
                if (cam.cropPos.y & 1)
                    mRaw.cfa.shiftDown();
            }

            CameraSensorInfo* sensor = cam.getSensorInfo(iso_speed);
            mRaw.blackLevel = sensor.mBlackLevel;
            mRaw.whitePoint = sensor.mWhiteLevel;
            mRaw.blackAreas = cam.blackAreas;
            if (mRaw.blackAreas.empty() && !sensor.mBlackLevelSeparate.empty())
            {
                if (mRaw.isCFA && mRaw.cfa.size.area() <= sensor.mBlackLevelSeparate.size())
                {
                    for (UInt32 i = 0; i < mRaw.cfa.size.area(); i++)
                    {
                        mRaw.blackLevelSeparate[i] = sensor.mBlackLevelSeparate[i];
                    }
                }
                else if (!mRaw.isCFA && mRaw.getCpp() <= sensor.mBlackLevelSeparate.size())
                {
                    for (UInt32 i = 0; i < mRaw.getCpp(); i++)
                    {
                        mRaw.blackLevelSeparate[i] = sensor.mBlackLevelSeparate[i];
                    }
                }
            }

            // Allow overriding individual blacklevels. Values are in CFA order
            // (the same order as the in the CFA tag)
            // A hint could be:
            // <Hint name="override_cfa_black" value="10,20,30,20"/>
            if (cam.hints.find(string("override_cfa_black")) != cam.hints.end())
            {
                string rgb = cam.hints.find(string("override_cfa_black")).second;
                vector<string> v = split_string(rgb, ',');
                if (v.size() != 4)
                {
                    mRaw.setError("Expected 4 values '10,20,30,20' as values for override_cfa_black hint.");
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        mRaw.blackLevelSeparate[i] = atoi(v[i].c_str());
                    }
                }
            }
        }


        void* RawDecoderDecodeThread(void* _this)
        {
            RawDecoderThread* me = (RawDecoderThread*)_this;
            try
            {
                me.parent.decodeThreaded(me);
            }
            catch (RawDecoderException &ex) {
                me.parent.mRaw.setError(ex.what());
            } catch (IOException &ex) {
                me.parent.mRaw.setError(ex.what());
            }
            return null;
            }

            void startThreads()
            {
# ifdef NO_PTHREAD
                UInt32 threads = 1;
                RawDecoderThread t;
                t.start_y = 0;
                t.end_y = mRaw.dim.y;
                t.parent = this;
                RawDecoderDecodeThread(&t);
#else
                UInt32 threads;
                bool fail = false;
                threads = Math.Min(mRaw.dim.y, getThreadCount());
                int y_offset = 0;
                int y_per_thread = (mRaw.dim.y + threads - 1) / threads;
                RawDecoderThread* t = new RawDecoderThread[threads];

                /* Initialize and set thread detached attribute */
                pthread_attr_t attr;
                pthread_attr_init(&attr);
                pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_JOINABLE);

                for (UInt32 i = 0; i < threads; i++)
                {
                    t[i].start_y = y_offset;
                    t[i].end_y = Math.Min(y_offset + y_per_thread, mRaw.dim.y);
                    t[i].parent = this;
                    if (pthread_create(&t[i].threadid, &attr, RawDecoderDecodeThread, &t[i]) != 0)
                    {
                        // If a failure occurs, we need to wait for the already created threads to finish
                        threads = i - 1;
                        fail = true;
                    }
                    y_offset = t[i].end_y;
                }

                for (UInt32 i = 0; i < threads; i++)
                {
                    pthread_join(t[i].threadid, null);
                }
                pthread_attr_destroy(&attr);
                delete[] t;

                if (fail)
                {
                    ThrowRDE("startThreads: Unable to start threads");
                }
#endif

                if (mRaw.errors.size() >= threads)
                    ThrowRDE("startThreads: All threads reported errors. Cannot load image.");
            }

            void decodeThreaded(RawDecoderThread* t)
            {
                ThrowRDE("Internal Error: This class does not support threaded decoding");
            }

            RawImage decodeRaw()
            {
                try
                {
                    RawImage raw = decodeRawInternal();
                    if (hints.find("pixel_aspect_ratio") != hints.end())
                    {
                        stringstream convert(hints.find("pixel_aspect_ratio").second);
            convert >> raw.metadata.pixelAspectRatio;
        }
            if (interpolateBadPixels)
                raw.fixBadPixels();
            return raw;
        }
        catch (TiffParserException &e) {
            ThrowRDE("%s", e.what());
} catch (FileIOException &e) {
            ThrowRDE("%s", e.what());
        } catch (IOException &e) {
            ThrowRDE("%s", e.what());
        }
        return null;
        }

        void decodeMetaData(CameraMetaData* meta)
{
    try
    {
        return decodeMetaDataInternal(meta);
    }
    catch (TiffParserException &e) {
        ThrowRDE("%s", e.what());
    } catch (FileIOException &e) {
        ThrowRDE("%s", e.what());
    } catch (IOException &e) {
        ThrowRDE("%s", e.what());
    }
    }

    void checkSupport(CameraMetaData* meta)
    {
        try
        {
            return checkSupportInternal(meta);
        }
        catch (TiffParserException &e) {
        ThrowRDE("%s", e.what());
    } catch (FileIOException &e) {
        ThrowRDE("%s", e.what());
    } catch (IOException &e) {
        ThrowRDE("%s", e.what());
    }
    }

    void startTasks(UInt32 tasks)
    {
        UInt32 threads;
        threads = Math.Min(tasks, getThreadCount());
        int ctask = 0;
        RawDecoderThread* t = new RawDecoderThread[threads];

        // We don't need a thread
        if (threads == 1)
        {
            t[0].parent = this;
            while ((UInt32)ctask < tasks)
            {
                t[0].taskNo = ctask++;
                try
                {
                    decodeThreaded(&t[0]);
                }
                catch (RawDecoderException &ex) {
        mRaw.setError(ex.what());
    } catch (IOException &ex) {
        mRaw.setError(ex.what());
    }
    }
    delete[] t;
    return;
}

# ifndef NO_PTHREAD
pthread_attr_t attr;

                        /* Initialize and set thread detached attribute */
                        pthread_attr_init(&attr);
                        pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_JOINABLE);

/* TODO: Create a way to re-use threads */
void* status;
                        while ((UInt32) ctask < tasks)
                        {
                            for (UInt32 i = 0; i<threads && (UInt32) ctask < tasks; i++)
                            {
    t [i].taskNo = ctask++;
    t [i].parent = this;
    pthread_create(&t [i].threadid, &attr, RawDecoderDecodeThread, &t [i]);
}
                            for (UInt32 i = 0; i<threads; i++)
                            {
                                pthread_join(t[i].threadid, &status);
                            }
                        }

                        if (mRaw.errors.size() >= tasks)
                            ThrowRDE("startThreads: All threads reported errors. Cannot load image.");

delete[] t;
#else
                        ThrowRDE("Unreachable");
#endif
                    }

                } // namespace RawSpeed
