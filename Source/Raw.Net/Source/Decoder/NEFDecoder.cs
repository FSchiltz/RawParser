using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RawNet
{
    public class NefSlice
    {
        public NefSlice() { h = offset = count = 0; }
        public UInt32 h;
        public UInt32 offset;
        public UInt32 count;
    };


    public class NefDecoder : RawDecoder
    {
        public IFD rootIFD;
        private TIFFBinaryReader reader;

        public NefDecoder(ref IFD rootIFD, TIFFBinaryReader file) : base(ref file)
        {
            this.rootIFD = (rootIFD);
            decoderVersion = 5;
            reader = file;
        }

        protected override byte[] decodeThumbInternal()
        {
            //find the preview ifd inside the makernote
            List<IFD> makernote = rootIFD.getIFDsWithTag((TagType)0x011);
            IFD preview = makernote[0].getIFDsWithTag((TagType)0x0201)[0];
            //no thumbnail
            if (preview == null) return null;

            var thumb = preview.getEntry((TagType)0x0201);
            var size = preview.getEntry((TagType)0x0202);
            if (size == null || thumb == null) return null;

            //get the makernote offset
            List<IFD> exifs = rootIFD.getIFDsWithTag((TagType)0x927C);

            if (exifs == null || exifs.Count == 0) return null;

            Tag makerNoteOffsetTag = exifs[0].getEntryRecursive((TagType)0x927C);
            if (makerNoteOffsetTag == null) return null;
            reader.Position = (uint)(thumb.data[0]) + 10 + makerNoteOffsetTag.dataOffset;
            return reader.ReadBytes(Convert.ToInt32(size.data[0]));
        }

        protected override RawImage decodeRawInternal()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: No image data found");

            IFD raw = data[0];
            int compression = raw.getEntry(TagType.COMPRESSION).getInt();

            data = rootIFD.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: No model data found");

            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);

            if (data[0].getEntry(TagType.MODEL).dataAsString == "NIKON D100 ")
            {  /**Sigh**/
                if (!mFile.isValid(offsets.getUInt()))
                    throw new RawDecoderException("NEF Decoder: Image data outside of file.");
                if (!D100IsCompressed(offsets.getUInt()))
                {
                    DecodeD100Uncompressed();
                    return mRaw;
                }
            }
            hints.TryGetValue("force_uncompressed", out string v);
            if (compression == 1 || (v != null) || NEFIsUncompressed(ref raw))
            {
                DecodeUncompressed();
                return mRaw;
            }

            if (NEFIsUncompressedRGB(ref raw))
            {
                DecodeSNefUncompressed();
                return mRaw;
            }

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("NEF Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("NEF Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
            }
            if (!mFile.isValid(offsets.getUInt(), counts.getUInt()))
                throw new RawDecoderException("NEF Decoder: Invalid strip byte count. File probably truncated.");


            if (34713 != compression)
                throw new RawDecoderException("NEF Decoder: Unsupported compression");

            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();
            UInt32 bitPerPixel = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();
            mRaw.colorDepth = (ushort)bitPerPixel;
            mRaw.dim = new iPoint2D((int)width, (int)height);

            data = rootIFD.getIFDsWithTag((TagType)0x8c);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: Decompression info tag not found");

            Tag meta;
            if (data[0].tags.ContainsKey(0x96))
            {
                meta = data[0].getEntry((TagType)0x96);
            }
            else
            {
                meta = data[0].getEntry((TagType)0x8c);  // Fall back
            }

            try
            {

                NikonDecompressor decompressor = new NikonDecompressor(mFile, mRaw);
                decompressor.uncorrectedRawValues = uncorrectedRawValues;
                TIFFBinaryReader metastream;
                if (data[0].endian == Endianness.little)
                    metastream = new TIFFBinaryReader(TIFFBinaryReader.streamFromArray(meta.data, (TiffDataType)meta.dataType));
                else
                    metastream = new TIFFBinaryReaderRE(TIFFBinaryReader.streamFromArray(meta.data, (TiffDataType)meta.dataType));

                //create a Linearisation to check
                //decompressor.table = new LinearisationTable((ushort)compression, (int)mRaw.bpp, offsets.getUInt(), 0, metastream, reader);

                decompressor.DecompressNikon(metastream, width, height, bitPerPixel, offsets.getUInt(), counts.getUInt());

            }
            catch (IOException e)
            {
                mRaw.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }

            return mRaw;
        }

        /*
        Figure out if a NEF file is compressed.  These fancy heuristics
        are only needed for the D100, thanks to a bug in some cameras
        that tags all images as "compressed".
        */
        bool D100IsCompressed(UInt32 offset)
        {
            int i;
            mFile.Position = offset;
            for (i = 15; i < 256; i += 16)
                if (mFile.ReadByte() != 0) return true;
            return false;
        }

        /* At least the D810 has a broken firmware that tags uncompressed images
           as if they were compressed. For those cases we set uncompressed mode
           by figuring out that the image is the size of uncompressed packing */
        bool NEFIsUncompressed(ref IFD raw)
        {
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();
            UInt32 bitPerPixel = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();

            return counts.getInt(0) == width * height * bitPerPixel / 8;
        }

        /* At least the D810 has a broken firmware that tags uncompressed images
           as if they were compressed. For those cases we set uncompressed mode
           by figuring out that the image is the size of uncompressed packing */
        bool NEFIsUncompressedRGB(ref IFD raw)
        {
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();

            return counts.getInt(0) == width * height * 3;
        }


        IFD FindBestImage(ref List<IFD> data)
        {
            int largest_width = 0;
            IFD best_ifd = null;
            for (int i = 0; i < data.Count; i++)
            {
                IFD raw = data[i];
                int width = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                if (width > largest_width)
                    best_ifd = raw;
            }
            if (null == best_ifd)
                throw new RawDecoderException("NEF Decoder: Unable to locate image");
            return best_ifd;
        }

        void DecodeUncompressed()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(ref data);
            UInt32 nslices = raw.getEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
            UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).getUInt();
            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();
            UInt32 bitPerPixel = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();

            List<NefSlice> slices = new List<NefSlice>();
            UInt32 offY = 0;

            for (UInt32 s = 0; s < nslices; s++)
            {
                NefSlice slice = new NefSlice();
                slice.offset = offsets.getUInt(s);
                slice.count = counts.getUInt(s);
                if (offY + yPerSlice > height)
                    slice.h = height - offY;
                else
                    slice.h = yPerSlice;

                offY = Math.Min(height, offY + yPerSlice);

                if (mFile.isValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            if (0 == slices.Count)
                throw new RawDecoderException("NEF Decoder: No valid slices found. File probably truncated.");

            mRaw.dim = new iPoint2D((int)width, (int)offY);
            if (bitPerPixel == 14 && width * slices[0].h * 2 == slices[0].count)
                bitPerPixel = 16; // D3 & D810
            hints.TryGetValue("real_bpp", out string v);
            if (v != hints.Last().Value)
            {
                bitPerPixel = UInt32.Parse(v);
            }

            bool bitorder = true;
            hints.TryGetValue("msb_override", out string v1);
            if (v1 != hints.Last().Value)
                bitorder = (v1 == "true");

            offY = 0;
            for (Int32 i = 0; i < slices.Count; i++)
            {
                NefSlice slice = slices[i];
                TIFFBinaryReader input = new TIFFBinaryReader(mFile.BaseStream, slice.offset, slice.count);
                iPoint2D size = new iPoint2D((int)width, (int)slice.h);
                iPoint2D pos = new iPoint2D(0, (int)offY);
                try
                {
                    hints.TryGetValue("coolpixmangled", out string mangled);
                    hints.TryGetValue("coolpixsplit", out string split);
                    if (mangled != hints.Last().Value)
                        readCoolpixMangledRaw(ref input, size, pos, (int)(width * bitPerPixel / 8));
                    else if (split != hints.Last().Value)
                        readCoolpixSplitRaw(ref input, size, pos, (int)(width * bitPerPixel / 8));
                    else
                        readUncompressedRaw(ref input, size, pos, (int)(width * bitPerPixel / 8), (int)bitPerPixel, ((bitorder) ? BitOrder.Jpeg : BitOrder.Plain));
                }
                catch (RawDecoderException e)
                {
                    if (i > 0)
                        mRaw.errors.Add(e.Message);
                    else
                        throw;
                }
                catch (IOException e)
                {
                    if (i > 0)
                        mRaw.errors.Add(e.Message);
                    else
                        throw new RawDecoderException("NEF decoder: IO error occurred in first slice, unable to decode more. Error is: " + e.Message);
                }
                offY += slice.h;
            }
        }


        void readCoolpixMangledRaw(ref TIFFBinaryReader input, iPoint2D size, iPoint2D offset, int inputPitch)
        {
            UInt32 outPitch = mRaw.pitch;
            UInt32 w = (uint)size.x;
            UInt32 h = (uint)size.y;
            UInt32 cpp = mRaw.cpp;
            if (input.getRemainSize() < (inputPitch * h))
            {
                if (input.getRemainSize() > inputPitch)
                    h = (uint)(input.getRemainSize() / inputPitch - 1);
                else
                    throw new FileIOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.y > mRaw.dim.y)
                throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
            if (offset.x + size.x > mRaw.dim.x)
                throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

            UInt32 y = (uint)offset.y;
            h = Math.Min(h + (UInt32)offset.y, (UInt32)mRaw.dim.y);
            w *= cpp;
            BitPumpMSB32 inputMSB = new BitPumpMSB32(input);
            for (; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x++)
                {
                    //TODO fix X
                    mRaw.rawData[x + (offset.x * sizeof(UInt16) * cpp + y * outPitch)] = (ushort)inputMSB.getBits(12);
                }
            }
        }

        void readCoolpixSplitRaw(ref TIFFBinaryReader input, iPoint2D size, iPoint2D offset, int inputPitch)
        {
            UInt32 outPitch = mRaw.pitch;
            UInt32 w = (uint)size.x;
            UInt32 h = (uint)size.y;
            UInt32 cpp = mRaw.cpp;
            if (input.getRemainSize() < (inputPitch * h))
            {
                if (input.getRemainSize() > inputPitch)
                    h = (uint)(input.getRemainSize() / inputPitch - 1);
                else
                    throw new FileIOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.y > mRaw.dim.y)
                throw new RawDecoderException("readCoolpixSplitRaw: Invalid y offset");
            if (offset.x + size.x > mRaw.dim.x)
                throw new RawDecoderException("readCoolpixSplitRaw: Invalid x offset");

            UInt32 y = (uint)offset.y;
            h = Math.Min(h + (UInt32)offset.y, (UInt32)mRaw.dim.y);
            w *= cpp;
            h /= 2;
            BitPumpMSB inputMSB = new BitPumpMSB(ref input);
            for (; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x++)
                {
                    mRaw.rawData[x + (offset.x * sizeof(UInt16) * cpp + y * 2 * outPitch)] = (ushort)inputMSB.getBits(12);
                }
            }
            for (y = (uint)offset.y; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x++)
                {
                    mRaw.rawData[x + (offset.x * sizeof(UInt16) * cpp + (y * 2 + 1) * outPitch)] = (ushort)inputMSB.getBits(12);
                }
            }
        }

        void DecodeD100Uncompressed()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count < 2)
                throw new RawDecoderException("DecodeD100Uncompressed: No image data found");

            IFD rawIFD = data[1];

            UInt32 offset = rawIFD.getEntry(TagType.STRIPOFFSETS).getUInt();
            // Hardcode the sizes as at least the width is not correctly reported
            uint w = 3040;
            uint h = 2024;

            mRaw.dim = new iPoint2D((int)w, (int)h);
            TIFFBinaryReader input = new TIFFBinaryReader(mFile.BaseStream, offset, (uint)mFile.BaseStream.Length);

            Decode12BitRawBEWithControl(ref input, w, h);
        }

        void DecodeSNefUncompressed()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(ref data);
            UInt32 offset = raw.getEntry(TagType.STRIPOFFSETS).getUInt();
            UInt32 w = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 h = raw.getEntry(TagType.IMAGELENGTH).getUInt();

            mRaw.dim = new iPoint2D((int)w, (int)h);
            mRaw.cpp = (3);
            mRaw.isCFA = false;

            TIFFBinaryReader input = new TIFFBinaryReader(mFile.BaseStream, offset, (uint)mFile.BaseStream.Length);

            DecodeNikonSNef(ref input, w, h);
        }

        protected override void checkSupportInternal(CameraMetaData meta)
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("NEF Support check: Model name not found");
            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;

            string mode = getMode();
            string extended_mode = getExtendedMode(mode);

            if (meta.hasCamera(make, model, extended_mode))
                this.checkCameraSupported(meta, make, model, extended_mode);
            else this.checkCameraSupported(meta, make, model, mode);
        }

        string getMode()
        {
            string mode = "";
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(ref data);
            int compression = raw.getEntry(TagType.COMPRESSION).getInt();
            UInt32 bitPerPixel = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();

            if (NEFIsUncompressedRGB(ref raw))
                mode += "sNEF-uncompressed";
            else
            {
                if (1 == compression || NEFIsUncompressed(ref raw))
                    mode += bitPerPixel + "bit-uncompressed";
                else
                    mode += bitPerPixel + "bit-compressed";
            }
            return mode;
        }

        string getExtendedMode(string mode)
        {
            string extended_mode = "";
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);
            if (data.Count == 0)
                throw new RawDecoderException("NEF Support check: Image size not found");
            if (!data[0].hasEntry(TagType.IMAGEWIDTH) || !data[0].hasEntry(TagType.IMAGELENGTH))
                throw new RawDecoderException("NEF Support: Image size not found");
            UInt32 width = data[0].getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = data[0].getEntry(TagType.IMAGELENGTH).getUInt();

            extended_mode += width + "x" + height + "-" + mode;
            return extended_mode;
        }

        override protected void decodeMetaDataInternal(CameraMetaData meta)
        {
            int iso = 0;
            mRaw.cfa.setCFA(new iPoint2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);

            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Meta Decoder: Model name not found");
            if (!data[0].hasEntry(TagType.MAKE))
                throw new RawDecoderException("NEF Support: Make name not found");

            uint white = mRaw.whitePoint;
            int black = mRaw.blackLevel;

            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;

            if (rootIFD.hasEntryRecursive(TagType.ISOSPEEDRATINGS))
                iso = rootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS).getInt();

            // Read the whitebalance

            // We use this for the D50 and D2X whacky WB "encryption"
            byte[] serialmap = {
  0xc1,0xbf,0x6d,0x0d,0x59,0xc5,0x13,0x9d,0x83,0x61,0x6b,0x4f,0xc7,0x7f,0x3d,0x3d,
  0x53,0x59,0xe3,0xc7,0xe9,0x2f,0x95,0xa7,0x95,0x1f,0xdf,0x7f,0x2b,0x29,0xc7,0x0d,
  0xdf,0x07,0xef,0x71,0x89,0x3d,0x13,0x3d,0x3b,0x13,0xfb,0x0d,0x89,0xc1,0x65,0x1f,
  0xb3,0x0d,0x6b,0x29,0xe3,0xfb,0xef,0xa3,0x6b,0x47,0x7f,0x95,0x35,0xa7,0x47,0x4f,
  0xc7,0xf1,0x59,0x95,0x35,0x11,0x29,0x61,0xf1,0x3d,0xb3,0x2b,0x0d,0x43,0x89,0xc1,
  0x9d,0x9d,0x89,0x65,0xf1,0xe9,0xdf,0xbf,0x3d,0x7f,0x53,0x97,0xe5,0xe9,0x95,0x17,
  0x1d,0x3d,0x8b,0xfb,0xc7,0xe3,0x67,0xa7,0x07,0xf1,0x71,0xa7,0x53,0xb5,0x29,0x89,
  0xe5,0x2b,0xa7,0x17,0x29,0xe9,0x4f,0xc5,0x65,0x6d,0x6b,0xef,0x0d,0x89,0x49,0x2f,
  0xb3,0x43,0x53,0x65,0x1d,0x49,0xa3,0x13,0x89,0x59,0xef,0x6b,0xef,0x65,0x1d,0x0b,
  0x59,0x13,0xe3,0x4f,0x9d,0xb3,0x29,0x43,0x2b,0x07,0x1d,0x95,0x59,0x59,0x47,0xfb,
  0xe5,0xe9,0x61,0x47,0x2f,0x35,0x7f,0x17,0x7f,0xef,0x7f,0x95,0x95,0x71,0xd3,0xa3,
  0x0b,0x71,0xa3,0xad,0x0b,0x3b,0xb5,0xfb,0xa3,0xbf,0x4f,0x83,0x1d,0xad,0xe9,0x2f,
  0x71,0x65,0xa3,0xe5,0x07,0x35,0x3d,0x0d,0xb5,0xe9,0xe5,0x47,0x3b,0x9d,0xef,0x35,
  0xa3,0xbf,0xb3,0xdf,0x53,0xd3,0x97,0x53,0x49,0x71,0x07,0x35,0x61,0x71,0x2f,0x43,
  0x2f,0x11,0xdf,0x17,0x97,0xfb,0x95,0x3b,0x7f,0x6b,0xd3,0x25,0xbf,0xad,0xc7,0xc5,
  0xc5,0xb5,0x8b,0xef,0x2f,0xd3,0x07,0x6b,0x25,0x49,0x95,0x25,0x49,0x6d,0x71,0xc7};
            byte[] keymap = {
  0xa7,0xbc,0xc9,0xad,0x91,0xdf,0x85,0xe5,0xd4,0x78,0xd5,0x17,0x46,0x7c,0x29,0x4c,
  0x4d,0x03,0xe9,0x25,0x68,0x11,0x86,0xb3,0xbd,0xf7,0x6f,0x61,0x22,0xa2,0x26,0x34,
  0x2a,0xbe,0x1e,0x46,0x14,0x68,0x9d,0x44,0x18,0xc2,0x40,0xf4,0x7e,0x5f,0x1b,0xad,
  0x0b,0x94,0xb6,0x67,0xb4,0x0b,0xe1,0xea,0x95,0x9c,0x66,0xdc,0xe7,0x5d,0x6c,0x05,
  0xda,0xd5,0xdf,0x7a,0xef,0xf6,0xdb,0x1f,0x82,0x4c,0xc0,0x68,0x47,0xa1,0xbd,0xee,
  0x39,0x50,0x56,0x4a,0xdd,0xdf,0xa5,0xf8,0xc6,0xda,0xca,0x90,0xca,0x01,0x42,0x9d,
  0x8b,0x0c,0x73,0x43,0x75,0x05,0x94,0xde,0x24,0xb3,0x80,0x34,0xe5,0x2c,0xdc,0x9b,
  0x3f,0xca,0x33,0x45,0xd0,0xdb,0x5f,0xf5,0x52,0xc3,0x21,0xda,0xe2,0x22,0x72,0x6b,
  0x3e,0xd0,0x5b,0xa8,0x87,0x8c,0x06,0x5d,0x0f,0xdd,0x09,0x19,0x93,0xd0,0xb9,0xfc,
  0x8b,0x0f,0x84,0x60,0x33,0x1c,0x9b,0x45,0xf1,0xf0,0xa3,0x94,0x3a,0x12,0x77,0x33,
  0x4d,0x44,0x78,0x28,0x3c,0x9e,0xfd,0x65,0x57,0x16,0x94,0x6b,0xfb,0x59,0xd0,0xc8,
  0x22,0x36,0xdb,0xd2,0x63,0x98,0x43,0xa1,0x04,0x87,0x86,0xf7,0xa6,0x26,0xbb,0xd6,
  0x59,0x4d,0xbf,0x6a,0x2e,0xaa,0x2b,0xef,0xe6,0x78,0xb6,0x4e,0xe0,0x2f,0xdc,0x7c,
  0xbe,0x57,0x19,0x32,0x7e,0x2a,0xd0,0xb8,0xba,0x29,0x00,0x3c,0x52,0x7d,0xa8,0x49,
  0x3b,0x2d,0xeb,0x25,0x49,0xfa,0xa3,0xaa,0x39,0xa7,0xc5,0xa7,0x50,0x11,0x36,0xfb,
  0xc6,0x67,0x4a,0xf5,0xa5,0x12,0x65,0x7e,0xb0,0xdf,0xaf,0x4e,0xb3,0x61,0x7f,0x2f};

            List<IFD> note = rootIFD.getIFDsWithTag((TagType)12);
            if (note.Count != 0)
            {
                Tag wb = note[0].getEntry((TagType)12);
                if (wb.dataCount == 4)
                {
                    mRaw.metadata.wbCoeffs[0] = wb.getFloat(0);
                    mRaw.metadata.wbCoeffs[1] = (float)Convert.ToDecimal(wb.data[2]);
                    mRaw.metadata.wbCoeffs[2] = (float)Convert.ToDecimal(wb.data[1]);
                    if (mRaw.metadata.wbCoeffs[1] == 0.0f)
                        mRaw.metadata.wbCoeffs[1] = 1.0f;
                }
            }
            else if (rootIFD.hasEntryRecursive((TagType)0x0097))
            {
                Tag wb = rootIFD.getEntryRecursive((TagType)0x0097);
                if (wb.dataCount > 4)
                {
                    UInt32 version = 0;
                    for (UInt32 i = 0; i < 4; i++)
                        version = (version << 4) + (uint)(wb.data[i]) - '0';
                    if (version == 0x100 && wb.dataCount >= 80 && wb.dataType == TiffDataType.UNDEFINED)
                    {
                        mRaw.metadata.wbCoeffs[0] = wb.getShort(36);
                        mRaw.metadata.wbCoeffs[2] = wb.getShort(37);
                        mRaw.metadata.wbCoeffs[1] = wb.getShort(38);
                    }
                    else if (version == 0x103 && wb.dataCount >= 26 && wb.dataType == TiffDataType.UNDEFINED)
                    {
                        mRaw.metadata.wbCoeffs[0] = wb.getShort(10);
                        mRaw.metadata.wbCoeffs[1] = wb.getShort(11);
                        mRaw.metadata.wbCoeffs[2] = wb.getShort(12);
                    }
                    else if (((version == 0x204 && wb.dataCount >= 564) ||
                              (version == 0x205 && wb.dataCount >= 284)) &&
                             rootIFD.hasEntryRecursive((TagType)0x001d) &&
                             rootIFD.hasEntryRecursive((TagType)0x00a7))
                    {
                        // Get the serial number
                        Tag serial = rootIFD.getEntryRecursive((TagType)0x001d);
                        UInt32 serialno = 0;
                        for (UInt32 i = 0; i < serial.dataCount; i++)
                        {
                            if ((byte)serial.data[i] == 0) break;
                            if ((byte)serial.data[i] >= (byte)'0' && (byte)serial.data[i] <= (byte)'9')
                                serialno = serialno * 10 + (uint)serial.data[i] - '0';
                            else
                                serialno = serialno * 10 + (uint)serial.data[(int)i] % 10;
                        }

                        // Get the decryption key
                        Tag key = rootIFD.getEntryRecursive((TagType)0x00a7);
                        UInt32 keyno = (uint)key.data[0] ^ (uint)key.data[1] ^ (uint)key.data[2] ^ (uint)key.data[3];

                        // "Decrypt" the block using the serial and key
                        uint bitOff = 4;
                        if (version == 0x204)
                            bitOff += 280;
                        byte ci = serialmap[serialno & 0xff];
                        byte cj = keymap[keyno & 0xff];
                        byte ck = 0x60;

                        byte[] buff = new byte[wb.dataCount];
                        for (UInt32 i = 0; i < 280; i++)
                            wb.data[i + bitOff] = (byte)((byte)wb.data[i + bitOff] ^ (cj += (byte)(ci * ck++)));

                        // Finally set the WB coeffs
                        UInt32 off = (uint)((version == 0x204) ? 6 : 14);
                        off += bitOff;
                        mRaw.metadata.wbCoeffs[0] = wb.get2BE(off);
                        mRaw.metadata.wbCoeffs[1] = wb.get2BE(off + 2);
                        mRaw.metadata.wbCoeffs[2] = wb.get2BE(off + 6);
                    }
                }
            }
            else if (rootIFD.hasEntryRecursive((TagType)0x0014))
            {
                Tag wb = rootIFD.getEntryRecursive((TagType)0x0014);

                if (wb.dataCount == 2560 && wb.dataType == TiffDataType.UNDEFINED)
                {
                    UInt32 red = (uint)wb.data[1249] | (((UInt32)wb.data[1248]) << 8);
                    UInt32 blue = (uint)wb.data[1251] | (((UInt32)wb.data[1250]) << 8);
                    mRaw.metadata.wbCoeffs[0] = red / 256.0f;
                    mRaw.metadata.wbCoeffs[1] = 1.0f;
                    mRaw.metadata.wbCoeffs[2] = blue / 256.0f;
                }
                else if (wb.dataAsString.StartsWith("NRW "))
                {
                    UInt32 offset = 0;
                    if (((string)(wb.dataAsString.Skip(4)) == "0100") && wb.dataCount > 72)
                        offset = 56;
                    else if (wb.dataCount > 1572)
                        offset = 1556;

                    if (offset != 0)
                    {
                        //TODO check iftag is byte type
                        mRaw.metadata.wbCoeffs[0] = wb.get4LE(offset) << 2;
                        mRaw.metadata.wbCoeffs[1] = wb.get4LE(offset + 4) + wb.get4LE(offset + 8);
                        mRaw.metadata.wbCoeffs[2] = wb.get4LE(offset + 12) << 2;
                    }
                }
            }

            hints.TryGetValue("nikon_wb_adjustment", out string vt);
            if (vt != null)
            {
                mRaw.metadata.wbCoeffs[0] *= (float)(256 / 527.0);
                mRaw.metadata.wbCoeffs[2] *= (float)(256 / 317.0);
            }

            //TODO replace with info from camer
            //decode Nikon metadata
            /*
               mRaw.metadata.make = make;
               mRaw.metadata.model = model;
               mRaw.metadata.mode = getMode();
               //iso

               */

            string mode = getMode();
            string extended_mode = getExtendedMode(mode);
            if (meta.hasCamera(make, model, extended_mode))
            {
                setMetaData(meta, make, model, extended_mode, iso);
            }
            else if (meta.hasCamera(make, model, mode))
            {
                setMetaData(meta, make, model, mode, iso);
            }
            else
            {
                setMetaData(meta, make, model, "", iso);
            }

            if (white != 65536)
                mRaw.whitePoint = white;
            hints.TryGetValue("nikon_override_auto_black", out string k);
            if (black >= 0 && k == null)
                mRaw.blackLevel = black;

        }


        // DecodeNikonYUY2 decodes 12 bit data in an YUY2-like pattern (2 Luma, 1 Chroma per 2 pixels).
        // We un-apply the whitebalance, so output matches lossless.
        // Note that values are scaled. See comment below on details.
        // OPTME: It would be trivial to run this multithreaded.
        void DecodeNikonSNef(ref TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 6) throw new FileIOException("NEF: got a " + w + " wide sNEF, aborting");

            UInt32 pitch = mRaw.pitch;
            if (input.getRemainSize() < (w * h * 3))
            {
                if ((UInt32)input.getRemainSize() > w * 3)
                {
                    h = (uint)(input.getRemainSize() / (w * 3) - 1);
                    mRaw.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new FileIOException("DecodeNikonSNef: Not enough data to decode a single line. Image file truncated.");
            }

            // We need to read the applied whitebalance, since we should return
            // data before whitebalance, so we "unapply" it.
            List<IFD> note = rootIFD.getIFDsWithTag((TagType)12);

            if (note.Count == 0)
                throw new RawDecoderException("NEF Decoder: Unable to locate whitebalance needed for decompression");

            Tag wb = note[0].getEntry((TagType)12);
            if (wb.dataCount != 4 || wb.dataType != TiffDataType.RATIONAL)
                throw new RawDecoderException("NEF Decoder: Whitebalance has unknown count or type");

            float wb_r = wb.getFloat(0);
            float wb_b = wb.getFloat(1);

            if (wb_r == 0.0f || wb_b == 0.0f)
                throw new RawDecoderException("NEF Decoder: Whitebalance has zero value");

            mRaw.metadata.wbCoeffs[0] = wb_r;
            mRaw.metadata.wbCoeffs[1] = 1.0f;
            mRaw.metadata.wbCoeffs[2] = wb_b;

            int inv_wb_r = (int)(1024.0 / wb_r);
            int inv_wb_b = (int)(1024.0 / wb_b);

            UInt16[] curve = gammaCurve(1 / 2.4, 12.92, 1, 4095);
            // Scale output values to 16 bits.
            for (int i = 0; i < 4096; i++)
            {
                int c = curve[i];
                curve[i] = (ushort)Common.clampbits(c << 2, 16);
            }
            mRaw.setTable(curve, 4095, true);

            UInt16 tmp = 0;
            ushort[] tmpch = new ushort[2];
            for (UInt32 y = 0; y < h; y++)
            {
                uint a1 = input.ReadByte();
                uint a2 = input.ReadByte();
                uint a3 = input.ReadByte();
                UInt32 random = a1 + (a2 << 8) + (a3 << 16);
                for (UInt32 x = 0; x < w * 3; x += 6)
                {
                    input.Position -= 3;
                    UInt32 g1 = input.ReadByte();
                    UInt32 g2 = input.ReadByte();
                    UInt32 g3 = input.ReadByte();
                    UInt32 g4 = input.ReadByte();
                    UInt32 g5 = input.ReadByte();
                    UInt32 g6 = input.ReadByte();

                    input.Position += 6;
                    float y1 = g1 | ((g2 & 0x0f) << 8);
                    float y2 = (g2 >> 4) | (g3 << 4);
                    float cb = g4 | ((g5 & 0x0f) << 8);
                    float cr = (g5 >> 4) | (g6 << 4);

                    float cb2 = cb;
                    float cr2 = cr;
                    // Interpolate right pixel. We assume the sample is aligned with left pixel.
                    if ((x + 6) < w * 3)
                    {
                        input.Position += 3;
                        g4 = input.ReadByte();
                        g5 = input.ReadByte();
                        g6 = input.ReadByte();
                        input.Position -= 3;
                        cb2 = ((g4 | ((g5 & 0x0f) << 8)) + cb) * 0.5f;
                        cr2 = (((g5 >> 4) | (g6 << 4)) + cr) * 0.5f;
                    }

                    cb -= 2048;
                    cr -= 2048;
                    cb2 -= 2048;
                    cr2 -= 2048;
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y1 + 1.370705 * cr), 12), ref tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    mRaw.rawData[x + (y * pitch)] = (ushort)Common.clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y1 - 0.337633 * cb - 0.698001 * cr), 12), ref mRaw.rawData, x + 1, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y1 + 1.732446 * cb), 12), ref tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    mRaw.rawData[x + 2 + (y * pitch)] = (ushort)Common.clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y2 + 1.370705 * cr2), 12), ref tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    mRaw.rawData[x + 3 + (y * pitch)] = (ushort)Common.clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y2 - 0.337633 * cb2 - 0.698001 * cr2), 12), ref mRaw.rawData, x + 4, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    mRaw.setWithLookUp((ushort)Common.clampbits((int)(y2 + 1.732446 * cb2), 12), ref tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    mRaw.rawData[x + 5 + (y * pitch)] = (ushort)Common.clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                }
            }
            mRaw.table = (null);
        }

        double SQR(double x) { return ((x) * (x)); }

        // From:  dcraw.c -- Dave Coffin's raw photo decoder
        UInt16[] gammaCurve(double pwr, double ts, int mode, int imax)
        {
            UInt16[] curve = new UInt16[65536];
            if (curve == null)
            {
                throw new RawDecoderException("NEF Decoder: Unable to allocate gamma curve");
            }
            int i;
            double[] g = new double[6], bnd = { 0, 0 };
            double r;
            g[0] = pwr;
            g[1] = ts;
            g[2] = g[3] = g[4] = 0;
            bnd[Convert.ToInt32(g[1] >= 1)] = 1;
            if (g[1] != 0 && (g[1] - 1) * (g[0] - 1) <= 0)
            {
                for (i = 0; i < 48; i++)
                {
                    g[2] = (bnd[0] + bnd[1]) / 2;
                    if (g[0] != 0) bnd[Convert.ToInt32((Math.Pow(g[2] / g[1], -g[0]) - 1) / g[0] - 1 / g[2] > -1)] = g[2];
                    else bnd[Convert.ToInt32(g[2] / Math.Exp(1 - 1 / g[2]) < g[1])] = g[2];
                }
                g[3] = g[2] / g[1];
                if (g[0] != 0) g[4] = g[2] * (1 / g[0] - 1);
            }
            if (g[0] != 0) g[5] = 1 / (g[1] * SQR(g[3]) / 2 - g[4] * (1 - g[3]) +
               (1 - Math.Pow(g[3], 1 + g[0])) * (1 + g[4]) / (1 + g[0])) - 1;
            else g[5] = 1 / (g[1] * SQR(g[3]) / 2 + 1
              - g[2] - g[3] - g[2] * g[3] * (Math.Log(g[3]) - 1)) - 1;

            if (mode == 1)
            {
                throw new RawDecoderException("NEF curve: Unimplemented mode");
            }
            for (i = 0; i < 0x10000; i++)
            {
                curve[i] = 0xffff;
                if ((r = (double)i / imax) < 1)
                {
                    curve[i] = (UInt16)(0x10000 * (mode != 0
                      ? (r < g[3] ? r * g[1] : (g[0] != 0 ? Math.Pow(r, g[0]) * (1 + g[4]) - g[4] : Math.Log(r) * g[2] + 1))
                      : (r < g[2] ? r / g[1] : (g[0] != 0 ? Math.Pow((r + g[4]) / (1 + g[4]), 1 / g[0]) : Math.Exp((r - 1) / g[2]))))
                    );
                }
            }
            return curve;
        }


        #region oldCode
        /*
        class NEFDecoder : TiffDecoder, IDisposable
            {
                protected NikonMakerNote makerNote;

                public override void Parse(Stream file)
                {
                    base.Parse(file);

                    Tag subifdoffsetTag;
                    if (!ifd.tags.TryGetValue(0x14A, out subifdoffsetTag)) throw new FormatException("File not correct");
                    subifd = new IFD[subifdoffsetTag.dataCount];
                    for (int i = 0; i < subifdoffsetTag.dataCount; i++)
                    {
                        subifd[i] = new IFD(fileStream, (uint)subifdoffsetTag.data[i], true, false);
                    }
                    //get the Exif
                    Tag exifoffsetTag;
                    if (!ifd.tags.TryGetValue(0x8769, out exifoffsetTag)) throw new FormatException("File not correct");
                    //todo third IFD
                    exif = new IFD(fileStream, (uint)exifoffsetTag.data[0], true, false);
                    Tag makerNoteOffsetTag;
                    if (!exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag)) throw new FormatException("File not correct");
                    makerNote = new NikonMakerNote(fileStream, makerNoteOffsetTag.dataOffset, true);
                }

                public override byte[] parseThumbnail()
                {
                    //Get the full size preview          
                    Tag thumbnailOffset, thumbnailSize;
                    if (makerNote?.preview != null && makerNote.preview.tags.TryGetValue(0x0201, out thumbnailOffset))
                    {
                        if (!makerNote.preview.tags.TryGetValue(0x0202, out thumbnailSize)) throw new FormatException("File not correct");
                        Tag makerNoteOffsetTag;
                        if (!exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag)) throw new FormatException("File not correct");
                        fileStream.Position = (uint)(thumbnailOffset.data[0]) + 10 + (uint)(makerNoteOffsetTag.dataOffset);
                        return fileStream.ReadBytes(Convert.ToInt32(thumbnailSize.data[0]));
                    }
                    else return null;
                }

                public override byte[] parsePreview()
                {
                    Tag imagepreviewOffsetTags, imagepreviewX, imagepreviewY, imagepreviewSize;
                    if (subifd[0].tags.TryGetValue(0x201, out imagepreviewOffsetTags))
                    {
                        if (!subifd[0].tags.TryGetValue(0x11A, out imagepreviewX)) throw new FormatException("File not correct");
                        if (!subifd[0].tags.TryGetValue(0x11B, out imagepreviewY)) throw new FormatException("File not correct");
                        if (!subifd[0].tags.TryGetValue(0x202, out imagepreviewSize)) throw new FormatException("File not correct");

                        //get the preview data ( faster than rezising )
                        fileStream.Position = (uint)imagepreviewOffsetTags.data[0];
                        return fileStream.ReadBytes(Convert.ToInt32(imagepreviewSize.data[0]));
                    }
                    else return null;
                }

                public override Dictionary<ushort, Tag> parseExif()
                {
                    Dictionary<ushort, Tag> temp = new Dictionary<ushort, Tag>();
                    Dictionary<ushort, ushort> nikonToStandard = new DictionnaryFromFileUShort(@"Assets\Dic\NikonToStandard.dic");
                    Dictionary<ushort, string> standardExifName = new DictionnaryFromFileString(@"Assets\\Dic\StandardExif.dic");
                    foreach (ushort exifTag in standardExifName.Keys)
                    {
                        Tag tempTag = null;
                        ushort nikonTagId;
                        if (nikonToStandard.TryGetValue(exifTag, out nikonTagId))
                        {
                            //
                            //ifd.tags.TryGetValue(nikonTagId, out tempTag);
                            //foreach (IFD ifd in subifd)
                            //{
                            //    if (tempTag == null) ifd.tags.TryGetValue(nikonTagId, out tempTag);
                            //}
                            //if (makerNote.preview != null && tempTag == null) makerNote.preview.tags.TryGetValue(nikonTagId, out tempTag);

                            if (makerNote.ifd != null && tempTag == null) makerNote.ifd.tags.TryGetValue(nikonTagId, out tempTag);
                            if (tempTag == null) exif?.tags.TryGetValue(nikonTagId, out tempTag);
                            //if (tempTag == null)
                           // {
                                //tempTag = new Tag
                                //{
                                //    dataType = 2,
                              //      data = { [0] = "Data not avalaible" }
                             //   };
                            //}
                            if (tempTag != null)
                            {
                                string t = "";
                                standardExifName.TryGetValue(exifTag, out t);
                                tempTag.displayName = t;

                                temp.Add(nikonTagId, tempTag);
                            }
                        }
                    }
                    return temp;
                }

                public override ushort[] parseRAWImage()
                {
                    //Get the RAW data info
                    Tag imageRAWOffsetTags, imageRAWWidth, imageRAWHeight, imageRAWSize, imageRAWCompressed, imageRAWDepth, imageRAWCFA;
                    int rawIFDNum = 1;
                    if (subifd.Length < 2) rawIFDNum = 0;
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0111, out imageRAWOffsetTags)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0117, out imageRAWSize)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x0103, out imageRAWCompressed)) throw new FormatException("File not correct");
                    if (!subifd[rawIFDNum].tags.TryGetValue(0x828e, out imageRAWCFA)) throw new FormatException("File not correct");
                    colorDepth = (ushort)imageRAWDepth.data[0];
                    height = (uint)imageRAWHeight.data[0];
                    width = (uint)imageRAWWidth.data[0];
                    cfa = new byte[4];
                    for (int i = 0; i < 4; i++) cfa[i] = (byte)imageRAWCFA.data[i];

                    Tag ContrastCurveTag;
                    if (makerNote.ifd.tags.TryGetValue(0x8C, out ContrastCurveTag))
                    {
                        curve = new double[ContrastCurveTag.dataCount];
                        for (int i = 0; i < ContrastCurveTag.dataCount; i++)
                        {
                            curve[i] = Convert.ToDouble(ContrastCurveTag.data[i]);
                        }
                    }

                    //get the colorBalance
                    Tag colorBalanceTag, colorLevelTag;
                    //first get the matrix of level for each pixel (a 2*2 array corresponding to the rgb bayer matrice used     
                    if (makerNote.ifd.tags.TryGetValue(0xc, out colorLevelTag))
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            camMul[((c << 1) | (c >> 1)) & 3] = (double)colorLevelTag.data[c];
                        }
                    }else if (makerNote.ifd.tags.TryGetValue(0x97, out colorBalanceTag))
                    {
                        int version = 0;
                        for (int i = 0; i < 4; i++)
                            version = version * 10 + (byte)(colorBalanceTag.data[i]) - '0';
                        if (version < 200)
                        {
                            switch (version)
                            {
                                case 100:
                                    for (int c = 0; c < 4; c++) camMul[(c >> 1) | ((c & 1) << 1)] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 72);
                                    break;
                                case 102:
                                    for (int c = 0; c < 4; c++) camMul[c ^ (c >> 1)] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 10);
                                    //check
                                    //for (int c = 0; c < 4; c++) sraw_mul[c ^ (c >> 1)] = get2();
                                    break;
                                case 103:
                                    for (int c = 0; c < 4; c++) camMul[c] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 20);
                                    break;
                            }
                        }
                        else
                        {
                            //encrypted
                            byte[][] xlat = new byte[2][] {
                            new byte [256]{ 0xc1,0xbf,0x6d,0x0d,0x59,0xc5,0x13,0x9d,0x83,0x61,0x6b,0x4f,0xc7,0x7f,0x3d,0x3d,
                              0x53,0x59,0xe3,0xc7,0xe9,0x2f,0x95,0xa7,0x95,0x1f,0xdf,0x7f,0x2b,0x29,0xc7,0x0d,
                              0xdf,0x07,0xef,0x71,0x89,0x3d,0x13,0x3d,0x3b,0x13,0xfb,0x0d,0x89,0xc1,0x65,0x1f,
                              0xb3,0x0d,0x6b,0x29,0xe3,0xfb,0xef,0xa3,0x6b,0x47,0x7f,0x95,0x35,0xa7,0x47,0x4f,
                              0xc7,0xf1,0x59,0x95,0x35,0x11,0x29,0x61,0xf1,0x3d,0xb3,0x2b,0x0d,0x43,0x89,0xc1,
                              0x9d,0x9d,0x89,0x65,0xf1,0xe9,0xdf,0xbf,0x3d,0x7f,0x53,0x97,0xe5,0xe9,0x95,0x17,
                              0x1d,0x3d,0x8b,0xfb,0xc7,0xe3,0x67,0xa7,0x07,0xf1,0x71,0xa7,0x53,0xb5,0x29,0x89,
                              0xe5,0x2b,0xa7,0x17,0x29,0xe9,0x4f,0xc5,0x65,0x6d,0x6b,0xef,0x0d,0x89,0x49,0x2f,
                              0xb3,0x43,0x53,0x65,0x1d,0x49,0xa3,0x13,0x89,0x59,0xef,0x6b,0xef,0x65,0x1d,0x0b,
                              0x59,0x13,0xe3,0x4f,0x9d,0xb3,0x29,0x43,0x2b,0x07,0x1d,0x95,0x59,0x59,0x47,0xfb,
                              0xe5,0xe9,0x61,0x47,0x2f,0x35,0x7f,0x17,0x7f,0xef,0x7f,0x95,0x95,0x71,0xd3,0xa3,
                              0x0b,0x71,0xa3,0xad,0x0b,0x3b,0xb5,0xfb,0xa3,0xbf,0x4f,0x83,0x1d,0xad,0xe9,0x2f,
                              0x71,0x65,0xa3,0xe5,0x07,0x35,0x3d,0x0d,0xb5,0xe9,0xe5,0x47,0x3b,0x9d,0xef,0x35,
                              0xa3,0xbf,0xb3,0xdf,0x53,0xd3,0x97,0x53,0x49,0x71,0x07,0x35,0x61,0x71,0x2f,0x43,
                              0x2f,0x11,0xdf,0x17,0x97,0xfb,0x95,0x3b,0x7f,0x6b,0xd3,0x25,0xbf,0xad,0xc7,0xc5,
                              0xc5,0xb5,0x8b,0xef,0x2f,0xd3,0x07,0x6b,0x25,0x49,0x95,0x25,0x49,0x6d,0x71,0xc7 },
                            new byte [256]{ 0xa7,0xbc,0xc9,0xad,0x91,0xdf,0x85,0xe5,0xd4,0x78,0xd5,0x17,0x46,0x7c,0x29,0x4c,
                              0x4d,0x03,0xe9,0x25,0x68,0x11,0x86,0xb3,0xbd,0xf7,0x6f,0x61,0x22,0xa2,0x26,0x34,
                              0x2a,0xbe,0x1e,0x46,0x14,0x68,0x9d,0x44,0x18,0xc2,0x40,0xf4,0x7e,0x5f,0x1b,0xad,
                              0x0b,0x94,0xb6,0x67,0xb4,0x0b,0xe1,0xea,0x95,0x9c,0x66,0xdc,0xe7,0x5d,0x6c,0x05,
                              0xda,0xd5,0xdf,0x7a,0xef,0xf6,0xdb,0x1f,0x82,0x4c,0xc0,0x68,0x47,0xa1,0xbd,0xee,
                              0x39,0x50,0x56,0x4a,0xdd,0xdf,0xa5,0xf8,0xc6,0xda,0xca,0x90,0xca,0x01,0x42,0x9d,
                              0x8b,0x0c,0x73,0x43,0x75,0x05,0x94,0xde,0x24,0xb3,0x80,0x34,0xe5,0x2c,0xdc,0x9b,
                              0x3f,0xca,0x33,0x45,0xd0,0xdb,0x5f,0xf5,0x52,0xc3,0x21,0xda,0xe2,0x22,0x72,0x6b,
                              0x3e,0xd0,0x5b,0xa8,0x87,0x8c,0x06,0x5d,0x0f,0xdd,0x09,0x19,0x93,0xd0,0xb9,0xfc,
                              0x8b,0x0f,0x84,0x60,0x33,0x1c,0x9b,0x45,0xf1,0xf0,0xa3,0x94,0x3a,0x12,0x77,0x33,
                              0x4d,0x44,0x78,0x28,0x3c,0x9e,0xfd,0x65,0x57,0x16,0x94,0x6b,0xfb,0x59,0xd0,0xc8,
                              0x22,0x36,0xdb,0xd2,0x63,0x98,0x43,0xa1,0x04,0x87,0x86,0xf7,0xa6,0x26,0xbb,0xd6,
                              0x59,0x4d,0xbf,0x6a,0x2e,0xaa,0x2b,0xef,0xe6,0x78,0xb6,0x4e,0xe0,0x2f,0xdc,0x7c,
                              0xbe,0x57,0x19,0x32,0x7e,0x2a,0xd0,0xb8,0xba,0x29,0x00,0x3c,0x52,0x7d,0xa8,0x49,
                              0x3b,0x2d,0xeb,0x25,0x49,0xfa,0xa3,0xaa,0x39,0xa7,0xc5,0xa7,0x50,0x11,0x36,0xfb,
                              0xc6,0x67,0x4a,0xf5,0xa5,0x12,0x65,0x7e,0xb0,0xdf,0xaf,0x4e,0xb3,0x61,0x7f,0x2f } };
                            Tag serialTag, shutterCountTag;
                            if (!makerNote.ifd.tags.TryGetValue(0x1D, out serialTag)) throw new FormatException("File not correct");
                            if (!makerNote.ifd.tags.TryGetValue(0xA7, out shutterCountTag)) throw new FormatException("File not correct");
                            byte[] buff = new byte[324];
                            for (int i = 0; i < 324; i++)
                            {
                                buff[i] = (byte)colorBalanceTag.data[i + 1];
                            }

                            //get serial
                            int serial = 0;
                            for (int i = 0; i < serialTag.data.Length; i++)
                            {
                                byte c = (byte)serialTag.dataAsString[i];
                                serial = (byte)(serial * 10) + (char.IsDigit((char)c) ? c - '0' : c % 10);
                            }
                            if (version < 217)
                            {
                                byte ci, cj, ck;
                                ci = xlat[0][serial & 0xff];
                                byte[] shutterAsByte = BitConverter.GetBytes((uint)shutterCountTag.data[0]);
                                cj = xlat[1][shutterAsByte[0] ^ shutterAsByte[1] ^ shutterAsByte[2] ^ shutterAsByte[3]];
                                ck = 0x60;
                                for (int i = 0; i < 324; i++)
                                {
                                    buff[i] ^= (cj += (byte)(ci * ck++));
                                }
                                int offset = "66666>666;6A;:;55"[version - 200] - '0';
                                for (int c = 0; c < 4; c++)
                                {
                                    camMul[c ^ (c >> 1) ^ (offset & 1)] = fileStream.readUshortFromArray(ref buff, (offset & -2) + c * 2);
                                }
                                for (int c = 0; c < 4; c++)
                                {
                                    camMul[c] = (camMul[c] / 0.93783628940582275) * 65535.0 / 16383;
                                }
                            }
                        }
                    }

                    ushort[] rawData;
                    //Check if uncompressed
                    if ((ushort)imageRAWCompressed.data[0] == 34713)
                    {
                        Tag compressionType;
                        if (!makerNote.ifd.tags.TryGetValue(0x0093, out compressionType)) throw new FormatException("File not correct");
                        //decompress the linearisationtable
                        Tag lineTag;
                        if (!makerNote.ifd.tags.TryGetValue(0x0096, out lineTag)) throw new FormatException("File not correct");
                        //uncompress the image
                        LinearisationTable line = new LinearisationTable((ushort)compressionType.data[0],
                            (ushort)imageRAWDepth.data[0], (uint)imageRAWOffsetTags.data[0],
                            lineTag.dataOffset + makerNote.getOffset(), fileStream);

                        makerNote = null;
                        rawData = line.uncompressed(height, width, cfa);
                        line.Dispose();
                    }
                    else
                    {
                        //get Raw Data            
                        fileStream.Position = (uint)imageRAWOffsetTags.data[0];
                        //TODO convert toushort from the byte table
                        //Normaly only nikon camera between D1 and d100 are not compressed
                        rawData = new ushort[height * width * 3];
                        var rawbyte = fileStream.ReadBytes(Convert.ToInt32(imageRAWSize.data[0]));
                        //todo implement
                        for (int i = 0; i < height * width; i++)
                        {
                            rawData[i] = rawbyte[i / 4];
                        }
                    }
                    fileStream.Dispose();
                    return rawData;
                }

                #region IDisposable Support
                private bool disposedValue = false; // To detect redundant calls

                protected virtual void Dispose(bool disposing)
                {
                    if (!disposedValue)
                    {
                        if (disposing)
                        {
                            // TODO: dispose managed state (managed objects).

                        }

                        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                        // TODO: set large fields to null.

                        disposedValue = true;
                    }
                }

                // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
                // ~NEFParser() {
                //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                //   Dispose(false);
                // }

                // This code added to correctly implement the disposable pattern.
                public void Dispose()
                {
                    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                    Dispose(true);
                    // TODO: uncomment the following line if the finalizer is overridden above.
                    // GC.SuppressFinalize(this);
                }
                #endregion
            }
        */
        #endregion
    }
}

