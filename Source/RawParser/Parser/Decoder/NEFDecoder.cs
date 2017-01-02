using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RawNet
{
    internal class NefSlice
    {
        public NefSlice() { h = offset = count = 0; }
        public UInt32 h;
        public UInt32 offset;
        public UInt32 count;
    };

    internal class NefDecoder : TiffDecoder
    {
        public NefDecoder(Stream file) : base(file)
        {
            ScaleValue = true;
        }

        public override Thumbnail DecodeThumb()
        {
            //find the preview ifd inside the makernote
            List<IFD> makernote = ifd.GetIFDsWithTag((TagType)0x011);
            if (makernote.Count != 0)
            {
                IFD preview = makernote[0].GetIFDsWithTag((TagType)0x0201)[0];
                //no thumbnail
                if (preview == null) return null;

                var thumb = preview.GetEntry((TagType)0x0201);
                var size = preview.GetEntry((TagType)0x0202);
                if (size == null || thumb == null) return null;

                //get the makernote offset
                List<IFD> exifs = ifd.GetIFDsWithTag((TagType)0x927C);

                if (exifs == null || exifs.Count == 0) return null;

                Tag makerNoteOffsetTag = exifs[0].GetEntryRecursive((TagType)0x927C);
                if (makerNoteOffsetTag == null) return null;
                reader.Position = (uint)(thumb.data[0]) + 10 + makerNoteOffsetTag.dataOffset;
                Thumbnail temp = new Thumbnail()
                {
                    data = reader.ReadBytes(size.GetInt(0)),
                    Type = ThumbnailType.JPEG,
                    dim = new Point2D()
                };
                return temp;
            }
            else
            {
                //no preview in the makernote, use the ifd0 preview
                uint bps = ifd.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);
                Point2D dim = new Point2D()
                {
                    width = ifd.GetEntry(TagType.IMAGEWIDTH).GetInt(0),
                    height = ifd.GetEntry(TagType.IMAGELENGTH).GetInt(0)
                };

                // Uncompressed
                uint cpp = ifd.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                if (cpp > 4)
                    throw new RawDecoderException();

                Tag offsets = ifd.GetEntry(TagType.STRIPOFFSETS);
                Tag counts = ifd.GetEntry(TagType.STRIPBYTECOUNTS);
                uint yPerSlice = ifd.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);

                reader.BaseStream.Position = offsets.GetInt(0);

                Thumbnail thumb = new Thumbnail()
                {
                    cpp = cpp,
                    dim = dim,
                    data = reader.ReadBytes(counts.GetInt(0)),
                    Type = ThumbnailType.RAW
                };
                return thumb;
            }
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: No image data found");

            IFD raw = data[0];
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);

            data = ifd.GetIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: No model data found");

            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);

            if (data[0].GetEntry(TagType.MODEL).DataAsString == "NIKON D100 ")
            {  /**Sigh**/
                if (!reader.IsValid(offsets.GetUInt(0)))
                    throw new RawDecoderException("NEF Decoder: Image data outside of file.");
                if (!D100IsCompressed(offsets.GetUInt(0)))
                {
                    DecodeD100Uncompressed();
                    return;
                }
            }
            hints.TryGetValue("force_uncompressed", out string v);
            if (compression == 1 || (v != null) || NEFIsUncompressed(raw))
            {
                DecodeUncompressed();
                return;
            }

            if (NEFIsUncompressedRGB(raw))
            {
                DecodeSNefUncompressed();
                return;
            }

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("NEF Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("NEF Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
            }
            if (!reader.IsValid(offsets.GetUInt(0), counts.GetUInt(0)))
                throw new RawDecoderException("NEF Decoder: Invalid strip byte count. File probably truncated.");

            if (34713 != compression)
                throw new RawDecoderException("NEF Decoder: Unsupported compression");

            rawImage.ColorDepth = raw.GetEntry(TagType.BITSPERSAMPLE).GetUShort(0);
            rawImage.raw.dim = new Point2D(raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0), raw.GetEntry(TagType.IMAGELENGTH).GetInt(0));

            data = ifd.GetIFDsWithTag((TagType)0x8c);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: Decompression info tag not found");

            Tag meta;
            if (data[0].tags.ContainsKey((TagType)0x96))
            {
                meta = data[0].GetEntry((TagType)0x96);
            }
            else
            {
                meta = data[0].GetEntry((TagType)0x8c);  // Fall back
            }

            rawImage.Init();
            try
            {
                NikonDecompressor decompressor = new NikonDecompressor(reader, rawImage);
                TIFFBinaryReader metastream;
                if (data[0].endian == Endianness.big)
                    metastream = new TIFFBinaryReaderRE(meta.data, meta.dataType);
                else
                    metastream = new TIFFBinaryReader(meta.data, meta.dataType);

                decompressor.DecompressNikon(metastream, offsets.GetUInt(0), counts.GetUInt(0));
                metastream.Dispose();
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }
        }

        /*
        Figure out if a NEF file is compressed.  These fancy heuristics
        are only needed for the D100, thanks to a bug in some cameras
        that tags all images as "compressed".
        */
        bool D100IsCompressed(UInt32 offset)
        {
            int i;
            reader.Position = offset;
            for (i = 15; i < 256; i += 16)
                if (reader.ReadByte() != 0) return true;
            return false;
        }

        /* At least the D810 has a broken firmware that tags uncompressed images
           as if they were compressed. For those cases we set uncompressed mode
           by figuring out that the image is the size of uncompressed packing */
        bool NEFIsUncompressed(IFD raw)
        {
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            UInt32 width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            UInt32 height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            UInt32 bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            return counts.GetInt(0) == width * height * bitPerPixel / 8;
        }

        /* At least the D810 has a broken firmware that tags uncompressed images
           as if they were compressed. For those cases we set uncompressed mode
           by figuring out that the image is the size of uncompressed packing */
        bool NEFIsUncompressedRGB(IFD raw)
        {
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            UInt32 width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            UInt32 height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

            return counts.GetInt(0) == width * height * 3;
        }

        IFD FindBestImage(List<IFD> data)
        {
            int largest_width = 0;
            IFD best_ifd = null;
            for (int i = 0; i < data.Count; i++)
            {
                IFD raw = data[i];
                int width = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
                if (width > largest_width)
                    best_ifd = raw;
            }
            if (null == best_ifd)
                throw new RawDecoderException("NEF Decoder: Unable to locate image");
            return best_ifd;
        }

        void DecodeUncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            UInt32 nslices = raw.GetEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            UInt32 yPerSlice = raw.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
            UInt32 width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            UInt32 height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            UInt32 bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            List<NefSlice> slices = new List<NefSlice>();
            UInt32 offY = 0;

            for (int s = 0; s < nslices; s++)
            {
                NefSlice slice = new NefSlice()
                {
                    offset = offsets.GetUInt(s),
                    count = counts.GetUInt(s)
                };
                if (offY + yPerSlice > height)
                    slice.h = height - offY;
                else
                    slice.h = yPerSlice;

                offY = Math.Min(height, offY + yPerSlice);

                if (reader.IsValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            if (0 == slices.Count)
                throw new RawDecoderException("NEF Decoder: No valid slices found. File probably truncated.");

            rawImage.raw.dim = new Point2D((int)width, (int)offY);
            if (bitPerPixel == 14 && width * slices[0].h * 2 == slices[0].count)
                bitPerPixel = 16; // D3 & D810
            hints.TryGetValue("real_bpp", out string v);
            if (v != null)
            {
                bitPerPixel = UInt32.Parse(v);
            }

            rawImage.ColorDepth = (ushort)bitPerPixel;
            bool bitorder = true;
            hints.TryGetValue("msb_override", out string v1);
            if (v1 != null)
                bitorder = (v1 == "true");

            offY = 0;
            //init the raw image
            rawImage.Init();
            for (Int32 i = 0; i < slices.Count; i++)
            {
                NefSlice slice = slices[i];
                TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, slice.offset);
                Point2D size = new Point2D((int)width, (int)slice.h);
                Point2D pos = new Point2D(0, (int)offY);
                try
                {
                    hints.TryGetValue("coolpixmangled", out string mangled);
                    hints.TryGetValue("coolpixsplit", out string split);
                    if (mangled != null)
                        ReadCoolpixMangledRaw(input, size, pos, (int)(width * bitPerPixel / 8));
                    else if (split != null)
                        ReadCoolpixSplitRaw(input, size, pos, (int)(width * bitPerPixel / 8));
                    else
                        ReadUncompressedRaw(input, size, pos, (int)(width * bitPerPixel / 8), (int)bitPerPixel, ((bitorder) ? BitOrder.Jpeg : BitOrder.Plain));
                }
                catch (RawDecoderException e)
                {
                    if (i > 0)
                        rawImage.errors.Add(e.Message);
                    else
                        throw;
                }
                catch (IOException e)
                {
                    if (i > 0)
                        rawImage.errors.Add(e.Message);
                    else
                        throw new RawDecoderException("NEF decoder: IO error occurred in first slice, unable to decode more. Error is: " + e.Message);
                }
                offY += slice.h;
            }
        }

        void ReadCoolpixMangledRaw(TIFFBinaryReader input, Point2D size, Point2D offset, int inputPitch)
        {
            // UInt32 outPitch = rawImage.pitch;
            int w = size.width;
            int h = size.height;
            int cpp = (int)rawImage.cpp;
            if (input.GetRemainSize() < (inputPitch * h))
            {
                if (input.GetRemainSize() > inputPitch)
                    h = (input.GetRemainSize() / inputPitch - 1);
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.height > rawImage.raw.dim.height)
                throw new RawDecoderException("readUncompressedRaw: Invalid y offset");
            if (offset.width + size.width > rawImage.raw.dim.width)
                throw new RawDecoderException("readUncompressedRaw: Invalid x offset");

            int y = offset.height;
            h = Math.Min(h + offset.height, rawImage.raw.dim.height);
            w *= cpp;
            BitPumpMSB32 inputMSB = new BitPumpMSB32(input);
            for (; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    //TODO fix X
                    rawImage.raw.data[x + (offset.width * sizeof(UInt16) * cpp + y * rawImage.raw.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
        }

        void ReadCoolpixSplitRaw(TIFFBinaryReader input, Point2D size, Point2D offset, int inputPitch)
        {
            //UInt32 outPitch = rawImage.pitch;
            int w = size.width;
            int h = size.height;
            int cpp = (int)rawImage.cpp;
            if (input.GetRemainSize() < (inputPitch * h))
            {
                if (input.GetRemainSize() > inputPitch)
                    h = input.GetRemainSize() / inputPitch - 1;
                else
                    throw new IOException("readUncompressedRaw: Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.height > rawImage.raw.dim.height)
                throw new RawDecoderException("readCoolpixSplitRaw: Invalid y offset");
            if (offset.width + size.width > rawImage.raw.dim.width)
                throw new RawDecoderException("readCoolpixSplitRaw: Invalid x offset");

            UInt32 y = (uint)offset.height;
            h = Math.Min(h + offset.height, rawImage.raw.dim.height);
            w *= cpp;
            h /= 2;
            BitPumpMSB inputMSB = new BitPumpMSB(input);
            for (; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x++)
                {
                    rawImage.raw.data[x + (offset.width * sizeof(UInt16) * cpp + y * 2 * rawImage.raw.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
            for (y = (uint)offset.height; y < h; y++)
            {
                for (UInt32 x = 0; x < w; x++)
                {
                    rawImage.raw.data[x + (offset.width * sizeof(UInt16) * cpp + (y * 2 + 1) * rawImage.raw.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
        }

        void DecodeD100Uncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count < 2)
                throw new RawDecoderException("DecodeD100Uncompressed: No image data found");

            IFD rawIFD = data[1];

            UInt32 offset = rawIFD.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
            // Hardcode the sizes as at least the width is not correctly reported
            int w = 3040;
            int h = 2024;
            rawImage.ColorDepth = 12;
            rawImage.raw.dim = new Point2D(w, h);
            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, offset);
            Decode12BitRawBEWithControl(input, w, h);
        }

        void DecodeSNefUncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            UInt32 offset = raw.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
            UInt32 w = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            UInt32 h = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

            rawImage.raw.dim = new Point2D((int)w, (int)h);
            rawImage.cpp = 3;
            rawImage.isCFA = false;
            rawImage.ColorDepth = 12;
            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, offset);

            DecodeNikonSNef(input, w, h);
        }

        string GetMode()
        {
            string mode = "";
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);
            UInt32 bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            if (NEFIsUncompressedRGB(raw))
                mode += "sNEF-uncompressed";
            else
            {
                if (1 == compression || NEFIsUncompressed(raw))
                    mode += bitPerPixel + "bit-uncompressed";
                else
                    mode += bitPerPixel + "bit-compressed";
            }
            return mode;
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();
            if (rawImage.metadata.Model == null)
                throw new RawDecoderException("NEF Meta Decoder: Model name not found");
            if (rawImage.metadata.Make == null)
                throw new RawDecoderException("NEF Support: Make name not found");
            if (rawImage.metadata.Model.Contains("NIKON")) rawImage.metadata.Model = rawImage.metadata.Model.Substring(6);

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
                  0xc5,0xb5,0x8b,0xef,0x2f,0xd3,0x07,0x6b,0x25,0x49,0x95,0x25,0x49,0x6d,0x71,0xc7
            };
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
                  0xc6,0x67,0x4a,0xf5,0xa5,0x12,0x65,0x7e,0xb0,0xdf,0xaf,0x4e,0xb3,0x61,0x7f,0x2f
            };

            List<IFD> note = ifd.GetIFDsWithTag((TagType)12);
            if (note.Count != 0)
            {
                Tag wb = note[0].GetEntry((TagType)12);
                if (wb.dataCount == 4)
                {
                    rawImage.metadata.WbCoeffs[0] = wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[1] = Convert.ToSingle(wb.data[2]);
                    rawImage.metadata.WbCoeffs[2] = Convert.ToSingle(wb.data[1]);
                    if (rawImage.metadata.WbCoeffs[1] == 0.0f)
                        rawImage.metadata.WbCoeffs[1] = 1.0f;
                }
            }
            else
            {
                Tag wb = ifd.GetEntryRecursive((TagType)0x0097);
                if (wb != null)
                {
                    if (wb.dataCount > 4)
                    {
                        UInt32 version = 0;
                        for (UInt32 i = 0; i < 4; i++)
                            version = (version << 4) + Convert.ToUInt32(wb.data[i]) - '0';
                        if (version == 0x100 && wb.dataCount >= 80 && wb.dataType == TiffDataType.UNDEFINED)
                        {
                            rawImage.metadata.WbCoeffs[0] = wb.GetShort(36);
                            rawImage.metadata.WbCoeffs[2] = wb.GetShort(37);
                            rawImage.metadata.WbCoeffs[1] = wb.GetShort(38);
                        }
                        else if (version == 0x103 && wb.dataCount >= 26 && wb.dataType == TiffDataType.UNDEFINED)
                        {
                            rawImage.metadata.WbCoeffs[0] = wb.GetShort(10);
                            rawImage.metadata.WbCoeffs[1] = wb.GetShort(11);
                            rawImage.metadata.WbCoeffs[2] = wb.GetShort(12);
                        }
                        else
                        {
                            if (((version == 0x204 && wb.dataCount >= 564) ||
                                (version == 0x205 && wb.dataCount >= 284)))
                            {
                                // Get the serial number
                                Tag serial = ifd.GetEntryRecursive((TagType)0x001d);
                                if (serial != null)
                                {
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
                                    Tag key = ifd.GetEntryRecursive((TagType)0x00a7);
                                    if (key != null)
                                    {
                                        UInt32 keyno = (uint)key.data[0] ^ (uint)key.data[1] ^ (uint)key.data[2] ^ (uint)key.data[3];

                                        // "Decrypt" the block using the serial and key
                                        uint bitOff = 4;
                                        if (version == 0x204)
                                            bitOff += 280;
                                        byte ci = serialmap[serialno & 0xff];
                                        byte cj = keymap[keyno & 0xff];
                                        byte ck = 0x60;

                                        for (UInt32 i = 0; i < 280; i++)
                                            wb.data[i + bitOff] = (byte)((byte)wb.data[i + bitOff] ^ (cj += (byte)(ci * ck++)));

                                        // Finally set the WB coeffs
                                        UInt32 off = (uint)((version == 0x204) ? 6 : 14);
                                        off += bitOff;
                                        rawImage.metadata.WbCoeffs[0] = wb.Get2BE(off);
                                        rawImage.metadata.WbCoeffs[1] = wb.Get2BE(off + 2);
                                        rawImage.metadata.WbCoeffs[2] = wb.Get2BE(off + 6);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    wb = ifd.GetEntryRecursive((TagType)0x0014);
                    if (wb != null)
                    {
                        if (wb.dataCount == 2560 && wb.dataType == TiffDataType.UNDEFINED)
                        {
                            UInt32 red = (uint)wb.data[1249] | (((UInt32)wb.data[1248]) << 8);
                            UInt32 blue = (uint)wb.data[1251] | (((UInt32)wb.data[1250]) << 8);
                            rawImage.metadata.WbCoeffs[0] = red / 256.0f;
                            rawImage.metadata.WbCoeffs[1] = 1.0f;
                            rawImage.metadata.WbCoeffs[2] = blue / 256.0f;
                        }
                        else if (wb.DataAsString.StartsWith("NRW "))
                        {
                            UInt32 offset = 0;
                            if (((string)(wb.DataAsString.Skip(4)) == "0100") && wb.dataCount > 72)
                                offset = 56;
                            else if (wb.dataCount > 1572)
                                offset = 1556;

                            if (offset != 0)
                            {
                                //TODO check iftag is byte type
                                rawImage.metadata.WbCoeffs[0] = wb.Get4LE(offset) << 2;
                                rawImage.metadata.WbCoeffs[1] = wb.Get4LE(offset + 4) + wb.Get4LE(offset + 8);
                                rawImage.metadata.WbCoeffs[2] = wb.Get4LE(offset + 12) << 2;
                            }
                        }
                    }
                }
            }

            string mode = GetMode();
            SetMetadata(rawImage.metadata.Model);
            rawImage.metadata.Mode = mode;

            //get cfa
            var cfa = ifd.GetEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                Debug.WriteLine("CFA pattern is not found");
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }

            if (rawImage.whitePoint > (1 << rawImage.ColorDepth) - 1)
                rawImage.whitePoint = (1 << rawImage.ColorDepth) - 1;

            //GPS data
            var gpsTag = ifd.GetEntry((TagType)0x0039);
            if (gpsTag != null)
            {
                using (var stream = new MemoryStream())
                {
                    byte[] info = new byte[4];
                    stream.Read(info, 0, 4);
                    //read the encoding
                    int encoding = stream.ReadByte();
                    //the data are in 70 bytes at index 9
                }
            }
        }

        // DecodeNikonYUY2 decodes 12 bit data in an YUY2-like pattern (2 Luma, 1 Chroma per 2 pixels).
        // We un-apply the whitebalance, so output matches lossless.
        // Note that values are scaled. See comment below on details.
        // TODO: It would be trivial to run this multithreaded.
        void DecodeNikonSNef(TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            if (w < 6) throw new IOException("NEF: got a " + w + " wide sNEF, aborting");

            //UInt32 pitch = rawImage.pitch;
            if (input.GetRemainSize() < (w * h * 3))
            {
                if ((UInt32)input.GetRemainSize() > w * 3)
                {
                    h = (uint)(input.GetRemainSize() / (w * 3) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("DecodeNikonSNef: Not enough data to decode a single line. Image file truncated.");
            }

            // We need to read the applied whitebalance, since we should return
            // data before whitebalance, so we "unapply" it.
            List<IFD> note = ifd.GetIFDsWithTag((TagType)12);

            if (note.Count == 0)
                throw new RawDecoderException("NEF Decoder: Unable to locate whitebalance needed for decompression");

            Tag wb = note[0].GetEntry((TagType)12);
            if (wb.dataCount != 4 || wb.dataType != TiffDataType.RATIONAL)
                throw new RawDecoderException("NEF Decoder: Whitebalance has unknown count or type");

            float wb_r = wb.GetFloat(0);
            float wb_b = wb.GetFloat(1);

            if (wb_r == 0.0f || wb_b == 0.0f)
                throw new RawDecoderException("NEF Decoder: Whitebalance has zero value");

            rawImage.metadata.WbCoeffs[0] = wb_r;
            rawImage.metadata.WbCoeffs[1] = 1.0f;
            rawImage.metadata.WbCoeffs[2] = wb_b;

            int inv_wb_r = (int)(1024.0 / wb_r);
            int inv_wb_b = (int)(1024.0 / wb_b);

            UInt16[] curve = GammaCurve(1 / 2.4, 12.92, 1, 4095);
            // Scale output values to 16 bits.
            for (int i = 0; i < 4096; i++)
            {
                int c = curve[i];
                curve[i] = (ushort)Common.Clampbits(c << 2, 16);
            }
            rawImage.SetTable(curve, 4095, true);

            UInt16 tmp = 0;
            ushort[] tmpch = new ushort[2];
            for (int y = 0; y < h; y++)
            {
                uint a1 = input.ReadByte();
                uint a2 = input.ReadByte();
                uint a3 = input.ReadByte();
                UInt32 random = a1 + (a2 << 8) + (a3 << 16);
                for (int x = 0; x < w * 3; x += 6)
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
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y1 + 1.370705 * cr), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.raw.data[x + (y * rawImage.raw.dim.width)] = (ushort)Common.Clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y1 - 0.337633 * cb - 0.698001 * cr), 12), rawImage.raw.data, x + 1, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y1 + 1.732446 * cb), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.raw.data[x + 2 + (y * rawImage.raw.dim.width)] = (ushort)Common.Clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 + 1.370705 * cr2), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.raw.data[x + 3 + (y * rawImage.raw.dim.width)] = (ushort)Common.Clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 - 0.337633 * cb2 - 0.698001 * cr2), 12), rawImage.raw.data, x + 4, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 + 1.732446 * cb2), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.raw.data[x + 5 + (y * rawImage.raw.dim.width)] = (ushort)Common.Clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                }
            }
            rawImage.table = (null);
        }

        double Sqr(double x) { return ((x) * (x)); }

        // From:  dcraw.c -- Dave Coffin's raw photo decoder
        UInt16[] GammaCurve(double pwr, double ts, int mode, int imax)
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
            if (g[0] != 0) g[5] = 1 / (g[1] * Sqr(g[3]) / 2 - g[4] * (1 - g[3]) +
               (1 - Math.Pow(g[3], 1 + g[0])) * (1 + g[4]) / (1 + g[0])) - 1;
            else g[5] = 1 / (g[1] * Sqr(g[3]) / 2 + 1
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

        /*
         * set specific metadata
         */
        protected void SetMetadata(string model)
        {
            switch (model.Trim())
            {
                case "D1":
                    rawImage.metadata.WbCoeffs[0] *= 256f / 527.0f;
                    rawImage.metadata.WbCoeffs[2] *= 256f / 317.0f;
                    break;
                case "D1X":
                    rawImage.raw.dim.width -= 4;
                    //pixel_aspect = 0.5;
                    break;
                case "D40X":
                case "D60":
                case "D80":
                case "D3000":
                    rawImage.raw.dim.height -= 3;
                    rawImage.raw.dim.width -= 4;
                    break;
                case "D3":
                case "D3S":
                case "D700":
                    rawImage.raw.dim.width -= 4;
                    rawImage.raw.offset.width = 2;
                    break;
                case "D3100":
                    rawImage.raw.dim.width -= 28;
                    rawImage.raw.offset.width = 6;
                    break;
                case "D5000":
                case "D90":
                    rawImage.raw.dim.width -= 42;
                    break;
                case "D5100":
                case "D7000":
                case "COOLPIX A":
                    rawImage.raw.dim.width -= 44;
                    break;
                case "D3200":
                case "D600":
                case "D610":
                case "D800":
                    rawImage.raw.dim.width -= 46;
                    break;
                case "D4":
                case "Df":
                    rawImage.raw.dim.width -= 52;
                    rawImage.raw.offset.width = 2;
                    break;
                case "D40":
                case "D50":
                case "D70":
                    rawImage.raw.dim.width--;
                    break;
                case "D100":
                    //if (load_flags)
                    //  raw_width = (width += 3) + 3;
                    break;
                case "D200":
                    rawImage.raw.offset.width = 1;
                    rawImage.raw.dim.width -= 4;
                    rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
                    break;
                case "D2H":
                    rawImage.raw.offset.width = 6;
                    rawImage.raw.dim.width -= 14;
                    break;
                case "D2X":
                    if (rawImage.raw.dim.width == 3264) rawImage.raw.dim.width -= 32;
                    else rawImage.raw.dim.width -= 8;
                    break;
                case "D300":
                    rawImage.raw.dim.width -= 32;
                    break;
                default: return;
            }
        }
    }
}

