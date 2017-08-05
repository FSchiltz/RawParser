using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RawNet.Decoder
{
    internal class NefSlice
    {
        public NefSlice() { h = offset = count = 0; }
        public uint h;
        public uint offset;
        public uint count;
    };

    internal class NefDecoder : TIFFDecoder
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

                var thumb = preview.GetEntry((TagType)0x0201)?.GetUInt(0) ?? 0;
                var size = preview.GetEntry((TagType)0x0202)?.GetInt(0) ?? 0;
                if (size == 0 || thumb == 0) return null;

                //get the makernote offset
                List<IFD> exifs = ifd.GetIFDsWithTag((TagType)0x927C);

                if (exifs == null || exifs.Count == 0) return null;

                Tag makerNoteOffsetTag = exifs[0].GetEntryRecursive((TagType)0x927C);
                if (makerNoteOffsetTag == null) return null;
                reader.Position = thumb + 10 + makerNoteOffsetTag.dataOffset;
                return new JPEGThumbnail(reader.ReadBytes(size));
            }
            else
            {
                //no preview in the makernote, use the ifd0 preview
                uint bps = ifd.GetEntry(TagType.BITSPERSAMPLE)?.GetUInt(0) ?? 8;
                Point2D dim = new Point2D()
                {
                    width = ifd.GetEntry(TagType.IMAGEWIDTH)?.GetUInt(0) ?? 0,
                    height = ifd.GetEntry(TagType.IMAGELENGTH)?.GetUInt(0) ?? 0
                };

                // Uncompressed
                uint cpp = ifd.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                if (cpp > 4)
                    throw new RawDecoderException();

                var offset = ifd.GetEntry(TagType.STRIPOFFSETS).GetInt(0);
                var count = ifd.GetEntry(TagType.STRIPBYTECOUNTS).GetInt(0);
                reader.BaseStream.Position = offset;

                Thumbnail thumb = new RAWThumbnail()
                {
                    cpp = cpp,
                    dim = dim,
                    data = reader.ReadBytes(count)
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
            if (compression == 1 || NEFIsUncompressed(raw))
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

            rawImage.fullSize.ColorDepth = raw.GetEntry(TagType.BITSPERSAMPLE).GetUShort(0);
            rawImage.fullSize.dim = new Point2D(raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0), raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0));

            data = ifd.GetIFDsWithTag((TagType)0x8c);

            if (data.Count == 0)
                throw new RawDecoderException("NEF Decoder: Decompression info tag not found");

            Tag meta;
            meta = data[0].GetEntry((TagType)0x96) ?? data[0].GetEntry((TagType)0x8c);
            rawImage.Init(false);
            try
            {
                NikonDecompressor decompressor = new NikonDecompressor(reader, rawImage);
                ImageBinaryReader metastream;
                if (data[0].endian == Endianness.Big)
                    metastream = new ImageBinaryReaderBigEndian(meta.data, meta.dataType);
                else
                    metastream = new ImageBinaryReader(meta.data, meta.dataType);

                decompressor.Decompress(metastream, offsets.GetUInt(0), counts.GetUInt(0));
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
        bool D100IsCompressed(uint offset)
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
            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            uint bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            return counts.GetInt(0) == width * height * bitPerPixel / 8;
        }

        /* At least the D810 has a broken firmware that tags uncompressed images
           as if they were compressed. For those cases we set uncompressed mode
           by figuring out that the image is the size of uncompressed packing */
        bool NEFIsUncompressedRGB(IFD raw)
        {
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

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
            if (best_ifd == null)
                throw new RawDecoderException("NEF Decoder: Unable to locate image");
            return best_ifd;
        }

        void DecodeUncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            uint nslices = raw.GetEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            uint yPerSlice = raw.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            uint bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            List<NefSlice> slices = new List<NefSlice>();
            uint offY = 0;

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

            rawImage.fullSize.ColorDepth = (ushort)bitPerPixel;
            rawImage.fullSize.dim = new Point2D(width, offY);
            if (bitPerPixel == 14 && width * slices[0].h * 2 == slices[0].count)
                bitPerPixel = 16; // D3 & D810

            bool bitorder = true;

            offY = 0;
            //init the raw image
            rawImage.Init(false);
            for (Int32 i = 0; i < slices.Count; i++)
            {
                NefSlice slice = slices[i];
                ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, slice.offset);
                Point2D size = new Point2D(width, slice.h);
                Point2D pos = new Point2D(0, offY);

                /*
                if (mangled != null)
                    ReadCoolpixMangledRaw(input, size, pos, (int)(width * bitPerPixel / 8));
                else if (split != null)
                    ReadCoolpixSplitRaw(input, size, pos, (int)(width * bitPerPixel / 8));
                else*/
                RawDecompressor.ReadUncompressedRaw(input, size, pos, (int)(width * bitPerPixel / 8), (int)bitPerPixel, ((bitorder) ? BitOrder.Jpeg : BitOrder.Plain), rawImage);
                offY += slice.h;
            }
        }

        void ReadCoolpixMangledRaw(ImageBinaryReader input, Point2D size, Point2D offset, int inputPitch)
        {
            // uint outPitch = rawImage.pitch;
            uint w = size.width;
            long h = size.height;
            uint cpp = rawImage.fullSize.cpp;
            if (input.RemainingSize < (inputPitch * h))
            {
                if (input.RemainingSize > inputPitch)
                    h = (input.RemainingSize / inputPitch - 1);
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.height > rawImage.fullSize.dim.height)
                throw new RawDecoderException("Invalid y offset");
            if (offset.width + size.width > rawImage.fullSize.dim.width)
                throw new RawDecoderException("Invalid x offset");

            uint y = offset.height;
            h = Math.Min(h + offset.height, rawImage.fullSize.dim.height);
            w *= cpp;
            BitPumpMSB32 inputMSB = new BitPumpMSB32(input);
            for (; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    //TODO fix X
                    rawImage.fullSize.rawView[x + (offset.width * sizeof(UInt16) * cpp + y * rawImage.fullSize.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
        }

        void ReadCoolpixSplitRaw(ImageBinaryReader input, Point2D size, Point2D offset, int inputPitch)
        {
            //uint outPitch = rawImage.pitch;
            uint w = size.width;
            long h = size.height;
            uint cpp = rawImage.fullSize.cpp;
            if (input.RemainingSize < (inputPitch * h))
            {
                if (input.RemainingSize > inputPitch)
                    h = input.RemainingSize / inputPitch - 1;
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            if (offset.height > rawImage.fullSize.dim.height)
                throw new RawDecoderException("Invalid y offset");
            if (offset.width + size.width > rawImage.fullSize.dim.width)
                throw new RawDecoderException("Invalid x offset");

            uint y = offset.height;
            h = Math.Min(h + offset.height, rawImage.fullSize.dim.height);
            w *= cpp;
            h /= 2;
            BitPumpMSB inputMSB = new BitPumpMSB(input);
            for (; y < h; y++)
            {
                for (uint x = 0; x < w; x++)
                {
                    rawImage.fullSize.rawView[x + (offset.width * sizeof(UInt16) * cpp + y * 2 * rawImage.fullSize.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
            for (y = offset.height; y < h; y++)
            {
                for (uint x = 0; x < w; x++)
                {
                    rawImage.fullSize.rawView[x + (offset.width * sizeof(UInt16) * cpp + (y * 2 + 1) * rawImage.fullSize.dim.width)] = (ushort)inputMSB.GetBits(12);
                }
            }
        }

        void DecodeD100Uncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count < 2)
                throw new RawDecoderException("DecodeD100Uncompressed: No image data found");

            IFD rawIFD = data[1];

            uint offset = rawIFD.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
            // Hardcode the sizes as at least the width is not correctly reported
            uint w = 3040;
            uint h = 2024;
            rawImage.fullSize.ColorDepth = 12;
            rawImage.fullSize.dim = new Point2D(w, h);
            ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, offset);
            RawDecompressor.Decode12BitRawBEWithControl(input, new Point2D(w, h), new Point2D(), rawImage);
        }

        void DecodeSNefUncompressed()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            uint offset = raw.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
            uint w = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint h = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

            rawImage.fullSize.dim = new Point2D(w, h);
            rawImage.fullSize.cpp = 3;
            rawImage.isCFA = false;
            rawImage.fullSize.ColorDepth = 12;
            ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, offset);

            DecodeNikonSNef(input, w, h);
        }

        string GetMode()
        {
            string mode = "";
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
            IFD raw = FindBestImage(data);
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);
            uint bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);

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

            //get the colorBalance
            Tag colorBalance = ifd.GetEntryRecursive((TagType)0x97);
            Tag oldColorBalance = ifd.GetEntryRecursive((TagType)0x0014);
            Tag colorLevel = ifd.GetEntryRecursive((TagType)0xc);
            //first get the matrix of level for each pixel (a 2*2 array corresponding to the rgb bayer matrice used     
            try
            {
                if (colorLevel != null)
                {
                    rawImage.metadata.WbCoeffs = new WhiteBalance(colorLevel.GetFloat(0), colorLevel.GetFloat(2), colorLevel.GetFloat(1));
                    if (rawImage.metadata.WbCoeffs.Green == 0.0f) rawImage.metadata.WbCoeffs.Green = 1.0f;
                }
                else if (colorBalance != null)
                {
                    int version = 0;
                    for (int i = 0; i < 4; i++)
                        version = version * 10 + colorBalance.GetByte(i) - '0';
                    if (version < 200)
                    {
                        //open a bitstream
                        ImageBinaryReader reader;
                        if (ifd.endian == Endianness.Big)
                        {
                            reader = new ImageBinaryReaderBigEndian(colorBalance.GetByteArray());
                        }
                        else
                        {
                            reader = new ImageBinaryReader(colorBalance.GetByteArray());
                        }

                        var wb = new ushort[4];
                        switch (version)
                        {
                            case 100:
                                reader.Position = 72;
                                for (int c = 0; c < 4; c++)
                                {
                                    wb[(c >> 1) | ((c & 1) << 1)] = reader.ReadUInt16();
                                }
                                break;
                            case 102:
                                reader.Position = 10;
                                for (int c = 0; c < 4; c++)
                                {
                                    wb[c ^ (c >> 1)] = reader.ReadUInt16();
                                }
                                break;
                            case 103:
                                reader.Position = 20;
                                for (int c = 0; c < 3; c++)
                                {
                                    wb[c] = reader.ReadUInt16();
                                }
                                break;
                        }
                        rawImage.metadata.WbCoeffs = new WhiteBalance(wb[0], wb[1], wb[2], rawImage.fullSize.ColorDepth);
                        reader.Dispose();
                    }
                    else
                    {
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

                        // Get the serial number
                        Tag serial = ifd.GetEntryRecursive((TagType)0x001d);
                        // Get the decryption key
                        Tag key = ifd.GetEntryRecursive((TagType)0x00a7);
                        if (serial == null || key == null) throw new FormatException("File not correct");

                        var colorInfo = colorBalance.GetByteArray();

                        uint serialno = 0;
                        for (int i = 0; i < serial.dataCount; i++)
                        {
                            byte serialI = serial.GetByte(i);
                            if (serialI == 0) break;
                            if (serialI >= (byte)'0' && serialI <= (byte)'9')
                                serialno = serialno * 10 + serialI - '0';
                            else
                                serialno = serialno * 10 + ((uint)serialI % 10);
                        }

                        uint keyno = key.GetUInt(0) ^ key.GetUInt(1) ^ key.GetUInt(2) ^ key.GetUInt(3);

                        // "Decrypt" the block using the serial and key
                        uint bitOff = 4;
                        if (version == 0x204)
                            bitOff += 280;
                        byte ci = serialmap[serialno & 0xff];
                        byte cj = keymap[keyno & 0xff];
                        byte ck = 0x60;

                        for (uint i = 0; i < 280; i++)
                            colorInfo[i + bitOff] = (byte)(colorInfo[i + bitOff] ^ (cj += (byte)(ci * ck++)));

                        // Finally set the WB coeffs
                        uint off = (uint)((version == 0x204) ? 6 : 14);
                        off += bitOff;
                        rawImage.metadata.WbCoeffs = new WhiteBalance(colorBalance.Get2BE(off), colorBalance.Get2BE(off + 2), colorBalance.Get2BE(off + 6));
                    }
                }
                else if (oldColorBalance != null)
                {
                    if (oldColorBalance.dataCount == 2560 && oldColorBalance.dataType == TiffDataType.UNDEFINED)
                    {
                        uint red = oldColorBalance.GetUInt(1249) | (oldColorBalance.GetUInt(1248) << 8);
                        uint blue = oldColorBalance.GetUInt(1251) | (oldColorBalance.GetUInt(1250) << 8);

                        rawImage.metadata.WbCoeffs = new WhiteBalance(red / 256.0f, 1.0f, blue / 256.0f);
                    }
                    else if (oldColorBalance.DataAsString.StartsWith("NRW "))
                    {
                        uint offset = 0;
                        if (((string)(oldColorBalance.DataAsString.Skip(4)) == "0100") && oldColorBalance.dataCount > 72)
                            offset = 56;
                        else if (oldColorBalance.dataCount > 1572)
                            offset = 1556;

                        if (offset != 0)
                        {
                            //TODO check if  tag is byte type
                            rawImage.metadata.WbCoeffs = new WhiteBalance(oldColorBalance.Get4LE(offset) << 2, oldColorBalance.Get4LE(offset + 4) + colorBalance.Get4LE(offset + 8), oldColorBalance.Get4LE(offset + 12) << 2);
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                //if we made it here, the data shoudl be correct
                rawImage.metadata.WbCoeffs = new WhiteBalance(1, 1, 1);
            }
            string mode = GetMode();
            SetMetadata(rawImage.metadata.Model);
            rawImage.metadata.Mode = mode;

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

            //get non Exif balck and white level         
            if (rawImage.whitePoint > (1 << rawImage.fullSize.ColorDepth) - 1)
                rawImage.whitePoint = (1 << rawImage.fullSize.ColorDepth) - 1;

            if (rawImage.black == 0)
            {
                rawImage.black = ifd.GetEntryRecursive((TagType)0x003d)?.GetUInt(0) ?? 0;
            }
        }

        // DecodeNikonYUY2 decodes 12 bit data in an YUY2-like pattern (2 Luma, 1 Chroma per 2 pixels).
        // We un-apply the whitebalance, so output matches lossless.
        // Note that values are scaled. See comment below on details.
        // TODO: It would be trivial to run this multithreaded.
        void DecodeNikonSNef(ImageBinaryReader input, uint w, uint h)
        {
            if (w < 6) throw new IOException("Got a " + w + " wide sNEF, aborting");

            //uint pitch = rawImage.pitch;
            if (input.RemainingSize < (w * h * 3))
            {
                if ((UInt32)input.RemainingSize > w * 3)
                {
                    h = (uint)(input.RemainingSize / (w * 3) - 1);
                    rawImage.errors.Add("Image truncated (file is too short)");
                }
                else
                    throw new IOException("Not enough data to decode a single line. Image file truncated.");
            }

            // We need to read the applied whitebalance, since we should return
            // data before whitebalance, so we "unapply" it.
            List<IFD> note = ifd.GetIFDsWithTag((TagType)12);

            if (note.Count == 0)
                throw new RawDecoderException("Unable to locate white balance needed for decompression");

            Tag wb = note[0].GetEntry((TagType)12);
            if (wb.dataCount != 4 || wb.dataType != TiffDataType.RATIONAL)
                throw new RawDecoderException("White balance has unknown count or type");

            float wb_r = wb.GetFloat(0);
            float wb_b = wb.GetFloat(1);

            if (wb_r == 0.0f || wb_b == 0.0f)
                throw new RawDecoderException("White balance has zero value");

            rawImage.metadata.WbCoeffs = new WhiteBalance(wb_r, 1, wb_b);

            int inv_wb_r = (int)(1024.0 / wb_r);
            int inv_wb_b = (int)(1024.0 / wb_b);

            UInt16[] curve = GammaCurve(1 / 2.4, 12.92, 1, 4095);
            // Scale output values to 16 bits.
            for (int i = 0; i < 4096; i++)
            {
                int c = curve[i];
                curve[i] = (ushort)Common.Clampbits(c << 2, 16);
            }
            rawImage.table = new TableLookUp(curve, 4095, true);

            UInt16 tmp = 0;
            ushort[] tmpch = new ushort[2];
            for (int y = 0; y < h; y++)
            {
                uint a1 = input.ReadByte();
                uint a2 = input.ReadByte();
                uint a3 = input.ReadByte();
                uint random = a1 + (a2 << 8) + (a3 << 16);
                for (int x = 0; x < w * 3; x += 6)
                {
                    input.Position -= 3;
                    uint g1 = input.ReadByte();
                    uint g2 = input.ReadByte();
                    uint g3 = input.ReadByte();
                    uint g4 = input.ReadByte();
                    uint g5 = input.ReadByte();
                    uint g6 = input.ReadByte();

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
                    rawImage.fullSize.rawView[x + (y * rawImage.fullSize.dim.width)] = (ushort)Common.Clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y1 - 0.337633 * cb - 0.698001 * cr), 12), rawImage.fullSize.rawView, x + 1, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y1 + 1.732446 * cb), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.fullSize.rawView[x + 2 + (y * rawImage.fullSize.dim.width)] = (ushort)Common.Clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 + 1.370705 * cr2), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.fullSize.rawView[x + 3 + (y * rawImage.fullSize.dim.width)] = (ushort)Common.Clampbits((inv_wb_r * tmp + (1 << 9)) >> 10, 15);

                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 - 0.337633 * cb2 - 0.698001 * cr2), 12), rawImage.fullSize.rawView, x + 4, ref random);
                    tmpch[0] = (byte)(tmp >> 8);
                    tmpch[1] = (byte)tmp;
                    rawImage.SetWithLookUp((ushort)Common.Clampbits((int)(y2 + 1.732446 * cb2), 12), tmpch, 0, ref random);
                    tmp = (ushort)(tmpch[0] << 8 + tmpch[1]);
                    rawImage.fullSize.rawView[x + 5 + (y * rawImage.fullSize.dim.width)] = (ushort)Common.Clampbits((inv_wb_b * tmp + (1 << 9)) >> 10, 15);
                }
            }
            rawImage.table = null;
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
            for (int i = 0; i < colorM.Length; i++)
            {
                if (colorM[i].name.Contains(model))
                {
                    rawImage.convertionM = colorM[i].matrix;
                    if (colorM[i].black != 0) rawImage.black = colorM[i].black;
                    if (colorM[i].white != 0) rawImage.whitePoint = colorM[i].white;
                    break;
                }
            }
            switch (model.Trim())
            {
                case "D1":
                    rawImage.metadata.WbCoeffs.Red *= 256f / 527.0f;
                    rawImage.metadata.WbCoeffs.Blue *= 256f / 317.0f;
                    break;
                case "D1X":
                    rawImage.fullSize.dim.width -= 4;
                    //pixel_aspect = 0.5;
                    break;
                case "D40X":
                case "D60":
                case "D80":
                case "D3000":
                    rawImage.fullSize.dim.height -= 3;
                    rawImage.fullSize.dim.width -= 4;
                    break;
                case "D3":
                case "D3S":
                case "D700":
                    rawImage.fullSize.dim.width -= 4;
                    rawImage.fullSize.offset.width = 2;
                    break;
                case "D3100":
                    rawImage.fullSize.dim.width -= 28;
                    rawImage.fullSize.offset.width = 6;
                    break;
                case "D5000":
                case "D90":
                    rawImage.fullSize.dim.width -= 42;
                    break;
                case "D5100":
                case "D7000":
                case "COOLPIX A":
                    rawImage.fullSize.dim.width -= 44;
                    break;
                case "D3200":
                case "D600":
                case "D610":
                case "D800":
                    rawImage.fullSize.dim.width -= 46;
                    break;
                case "D4":
                case "Df":
                    rawImage.fullSize.dim.width -= 52;
                    rawImage.fullSize.offset.width = 2;
                    break;
                case "D40":
                case "D50":
                case "D70":
                    rawImage.fullSize.dim.width--;
                    break;
                case "D100":
                    //if (load_flags)
                    //  raw_width = (width += 3) + 3;
                    break;
                case "D200":
                    rawImage.fullSize.offset.width = 1;
                    rawImage.fullSize.dim.width -= 4;
                    rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.Red, CFAColor.Green, CFAColor.Green, CFAColor.Blue);
                    break;
                case "D2H":
                    rawImage.fullSize.offset.width = 6;
                    rawImage.fullSize.dim.width -= 14;
                    break;
                case "D2X":
                    if (rawImage.fullSize.dim.width == 3264) rawImage.fullSize.dim.width -= 32;
                    else rawImage.fullSize.dim.width -= 8;
                    break;
                case "D300":
                    rawImage.fullSize.dim.width -= 32;
                    break;
                default: return;
            }
        }

        private CamRGB[] colorM = { 
            /*{ "Nikon D100", 0, 0,    { 5902,-933,-782,-8983,16719,2354,-1402,1455,6464 } },
    { "Nikon D1H", 0, 0,    { 7577,-2166,-926,-7454,15592,1934,-2377,2808,8606 } },
    { "Nikon D1X", 0, 0,    { 7702,-2245,-975,-9114,17242,1875,-2679,3055,8521 } },
    { "Nikon D1", 0, 0, // multiplied by 2.218750, 1.0, 1.148438 
	{ 16772,-4726,-2141,-7611,15713,1972,-2846,3494,9521 } },*/
     new CamRGB( "Nikon D200", 0, 0xfbc, new double[]   {8367,-2248,-763,-8758,16447,2422,-1527,1550,8053  }),/*
    { "Nikon D2H", 0, 0,    { 5710,-901,-615,-8594,16617,2024,-2975,4120,6830 } },
    { "Nikon D2X", 0, 0,    { 10231,-2769,-1255,-8301,15900,2552,-797,680,7148 } },
    { "Nikon D3000", 0, 0,    { 8736,-2458,-935,-9075,16894,2251,-1354,1242,8263 } },*/
    new CamRGB(        "Nikon D3100",0,0 ,       new double[,]{ { 7911, -2167, -813 },{ -5327, 13150, 2408 },{ -1288, 2483, 7968 } } ),
    new CamRGB(  "Nikon D3200", 0, 0xfb9,  new double[]  { 7013,-1408,-635,-5268,12902,2640,-1470,2801,7379  }),/*
    { "Nikon D3300", 0, 0,    { 6988,-1384,-714,-5631,13410,2447,-1485,2204,7318 } },
    { "Nikon D300", 0, 0,    { 9030,-1992,-715,-8465,16302,2255,-2689,3217,8069 } },
    { "Nikon D3X", 0, 0,    { 7171,-1986,-648,-8085,15555,2718,-2170,2512,7457 } },
    { "Nikon D3S", 0, 0,    { 8828,-2406,-694,-4874,12603,2541,-660,1509,7587 } },
    { "Nikon D3", 0, 0,    { 8139,-2171,-663,-8747,16541,2295,-1925,2008,8093 } },
    { "Nikon D40X", 0, 0,    { 8819,-2543,-911,-9025,16928,2151,-1329,1213,8449 } },
    { "Nikon D40", 0, 0,    { 6992,-1668,-806,-8138,15748,2543,-874,850,7897 } },
    { "Nikon D4S", 0, 0,    { 8598,-2848,-857,-5618,13606,2195,-1002,1773,7137 } },
    { "Nikon D4", 0, 0,    { 8598,-2848,-857,-5618,13606,2195,-1002,1773,7137 } },
    { "Nikon Df", 0, 0,    { 8598,-2848,-857,-5618,13606,2195,-1002,1773,7137 } },*/
    new CamRGB( "Nikon D500", 0, 0, new double[]   { 8813,-3210,-1036,-4703,12868,2021,-1054,1940,6129 } ),
     new CamRGB( "Nikon D5000", 0, 0xf00,   new double[]{  7309,-1403,-519,-8474,16008,2622,-2433,2826,8064  }),
    new CamRGB( "Nikon D5100", 0, 0x3de6,  new double[]{   8198,-2239,-724,-4871,12389,2798,-1043,2050,7181  }),/*
    { "Nikon D5200", 0, 0,
    { 8322,-3112,-1047,-6367,14342,2179,-988,1638,6394 } },
    { "Nikon D5300", 0, 0,
    { 6988,-1384,-714,-5631,13410,2447,-1485,2204,7318 } },
    { "Nikon D5500", 0, 0,
    { 8821,-2938,-785,-4178,12142,2287,-824,1651,6860 } },,
    { "Nikon D50", 0, 0,
    { 7732,-2422,-789,-8238,15884,2498,-859,783,7330 } },
    { "Nikon D5", 0, 0,
    { 9200,-3522,-992,-5755,13803,2117,-753,1486,6338 } },
    { "Nikon D600", 0, 0x3e07,
    { 8178,-2245,-609,-4857,12394,2776,-1207,2086,7298 } },
    { "Nikon D610", 0, 0,
    { 8178,-2245,-609,-4857,12394,2776,-1207,2086,7298 } },
    { "Nikon D60", 0, 0,
    { 8736,-2458,-935,-9075,16894,2251,-1354,1242,8263 } },
    { "Nikon D7000", 0, 0,
    { 8198,-2239,-724,-4871,12389,2798,-1043,2050,7181 } },*/
    new CamRGB(     "Nikon D7100",0,0,         new double[,]{ { 8322 , -3112 , -1047 },            { -6367 , 14342 , 2179  },           { -988 , 1638 , 6394  } }),
            /*,
    { "Nikon D7200", 0, 0,
    { 8322,-3112,-1047,-6367,14342,2179,-988,1638,6394 } },
    { "Nikon D750", 0, 0,
    { 9020,-2890,-715,-4535,12436,2348,-934,1919,7086 } },
    { "Nikon D700", 0, 0,
    { 8139,-2171,-663,-8747,16541,2295,-1925,2008,8093 } },
    { "Nikon D70", 0, 0,
    { 7732,-2422,-789,-8238,15884,2498,-859,783,7330 } },
    { "Nikon D810", 0, 0,
    { 9369,-3195,-791,-4488,12430,2301,-893,1796,6872 } },
    { "Nikon D800", 0, 0,
    { 7866,-2108,-555,-4869,12483,2681,-1176,2069,7501 } },
    { "Nikon D80", 0, 0,    { 8629,-2410,-883,-9055,16940,2171,-1490,1363,8520 } },*/
     new CamRGB( "Nikon D90", 0, 0xf00, new double[]  { 7309,-1403,-519,-8474,16008,2622,-2434,2826,8064  }),
     new CamRGB( "Nikon E700", 0, 0x3dd,    new double[]{   -3746,10611,1665,9621,-1734,2114,-2389,7082,3064,3406,6116,-244  }),
     new CamRGB( "Nikon E800", 0, 0x3dd,    new double[]{   -3746,10611,1665,9621,-1734,2114,-2389,7082,3064,3406,6116,-244  }),
     new CamRGB( "Nikon E950", 0, 0x3dd,    new double[]{   -3746,10611,1665,9621,-1734,2114,-2389,7082,3064,3406,6116,-244  }),/*
    { "Nikon E995", 0, 0,	// copied from E5000 
	{ -5547,11762,2189,5814,-558,3342,-4924,9840,5949,688,9083,96 } },
    { "Nikon E2100", 0, 0,	// copied from Z2, new white balance 
	{ 13142,-4152,-1596,-4655,12374,2282,-1769,2696,6711} },
    { "Nikon E2500", 0, 0,
    { -5547,11762,2189,5814,-558,3342,-4924,9840,5949,688,9083,96 } },
    { "Nikon E3200", 0, 0,		// DJC 
	{ 9846,-2085,-1019,-3278,11109,2170,-774,2134,5745 } },
    { "Nikon E4300", 0, 0,	// copied from Minolta DiMAGE Z2 
    { 11280,-3564,-1370,-4655,12374,2282,-1423,2168,5396 } },
    { "Nikon E4500", 0, 0,
    { -5547,11762,2189,5814,-558,3342,-4924,9840,5949,688,9083,96 } },
    { "Nikon E5000", 0, 0,
    { -5547,11762,2189,5814,-558,3342,-4924,9840,5949,688,9083,96 } },
    { "Nikon E5400", 0, 0,
    { 9349,-2987,-1001,-7919,15766,2266,-2098,2680,6839 } },
    { "Nikon E5700", 0, 0,
    { -5368,11478,2368,5537,-113,3148,-4969,10021,5782,778,9028,211 } },
    { "Nikon E8400", 0, 0,
    { 7842,-2320,-992,-8154,15718,2599,-1098,1342,7560 } },
    { "Nikon E8700", 0, 0,
    { 8489,-2583,-1036,-8051,15583,2643,-1307,1407,7354 } },
    { "Nikon E8800", 0, 0,
    { 7971,-2314,-913,-8451,15762,2894,-1442,1520,7610 } },
    { "Nikon COOLPIX A", 0, 0,
    { 8198,-2239,-724,-4871,12389,2798,-1043,2050,7181 } },*/
     new CamRGB( "Nikon COOLPIX P330", 200, 0,   new double[]{  10321,-3920,-931,-2750,11146,1824,-442,1545,5539  }),
     new CamRGB( "Nikon COOLPIX P340", 200, 0,   new double[]{  10321,-3920,-931,-2750,11146,1824,-442,1545,5539  }),/*
    { "Nikon COOLPIX P6000", 0, 0,
    { 9698,-3367,-914,-4706,12584,2368,-837,968,5801 } },
    { "Nikon COOLPIX P7000", 0, 0,
    { 11432,-3679,-1111,-3169,11239,2202,-791,1380,4455 } },
    { "Nikon COOLPIX P7100", 0, 0,
    { 11053,-4269,-1024,-1976,10182,2088,-526,1263,4469 } },*/
     new CamRGB( "Nikon COOLPIX P7700", 200, 0,    new double[]{ 10321,-3920,-931,-2750,11146,1824,-442,1545,5539  }),
     new CamRGB("Nikon COOLPIX P7800", 200, 0,    new double[]{ 10321,-3920,-931,-2750,11146,1824,-442,1545,5539 }),/*
    { "Nikon 1 V3", 0, 0,
    { 5958,-1559,-571,-4021,11453,2939,-634,1548,5087 } },
    { "Nikon 1 J4", 0, 0,
    { 5958,-1559,-571,-4021,11453,2939,-634,1548,5087 } },
    { "Nikon 1 J5", 0, 0,
    { 7520,-2518,-645,-3844,12102,1945,-913,2249,6835 } },*/

     new CamRGB( "Nikon 1 S2", 200, 0,  new double[]  { 6612,-1342,-618,-3338,11055,2623,-174,1792,5075 } ),/*
    { "Nikon 1 V2", 0, 0,
    { 6588,-1305,-693,-3277,10987,2634,-355,2016,5106 } },
    { "Nikon 1 J3", 0, 0,
    { 6588,-1305,-693,-3277,10987,2634,-355,2016,5106 } },
    { "Nikon 1 AW1", 0, 0,
    { 6588,-1305,-693,-3277,10987,2634,-355,2016,5106 } },
    { "Nikon 1 ", 0, 0,		// J1, J2, S1, V1
	{ 8994,-2667,-865,-4594,12324,2552,-699,1786,6260 } }*/};
    }
}
