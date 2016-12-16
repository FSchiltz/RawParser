using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class OrfDecoder : TiffDecoder
    {
        internal OrfDecoder(ref Stream file) : base(ref file) { }

        public override Thumbnail DecodeThumb()
        {
            //find the preview ifd Preview is in the rootIFD (smaller preview in subiFD use those)
            List<IFD> possible = ifd.GetIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT);
            //no thumbnail
            if (possible == null || possible.Count == 0) return null;
            IFD preview = possible[possible.Count - 1];

            var thumb = preview.GetEntry(TagType.JPEGINTERCHANGEFORMAT);
            var size = preview.GetEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
            if (size == null || thumb == null) return null;

            reader.Position = (uint)(thumb.data[0]);
            Thumbnail temp = new Thumbnail()
            {
                data = reader.ReadBytes(Convert.ToInt32(size.data[0])),
                Type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count == 0)
                throw new RawDecoderException("ORF Decoder: No image data found");

            IFD raw = data[0];
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);
            if (1 != compression)
                throw new RawDecoderException("ORF Decoder: Unsupported compression");

            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            if (counts.dataCount != offsets.dataCount)
                throw new RawDecoderException("ORF Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);

            uint off = raw.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
            uint size = 0;
            for (int i = 0; i < counts.dataCount; i++)
                size += counts.GetUInt(i);

            if (!reader.IsValid(off, size))
                throw new RawDecoderException("ORF Decoder: Truncated file");

            Int32 width = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            Int32 height = raw.GetEntry(TagType.IMAGELENGTH).GetInt(0);

            rawImage.dim = new Point2D(width, height);
            rawImage.Init();

            // We add 3 bytes slack, since the bitpump might be a few bytes ahead.
            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, off);
            input.BaseStream.Position = off;
            try
            {
                if (offsets.dataCount != 1 || (hints.ContainsKey("force_uncompressed")))
                    DecodeUncompressed(input, width, height, size, raw.endian);
                else
                    DecodeCompressed(input, width, height);
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
            }
        }

        private void DecodeUncompressed(TIFFBinaryReader s, int w, int h, uint size, Endianness endian)
        {
            if ((hints.ContainsKey("packed_with_control")))
                Decode12BitRawWithControl(s, w, h);
            else if ((hints.ContainsKey("jpeg32_bitorder")))
            {
                Point2D dim = new Point2D(w, h), pos = new Point2D(0, 0);
                ReadUncompressedRaw(ref s, dim, pos, w * 12 / 8, 12, BitOrder.Jpeg32);
            }
            else if (size >= w * h * 2)
            { // We're in an unpacked raw
                if (endian == Endianness.little)
                    Decode12BitRawUnpacked(s, w, h);
                else
                    Decode12BitRawBEunpackedLeftAligned(s, w, h);
            }
            else if (size >= w * h * 3 / 2)
            { // We're in one of those weird interlaced packed raws
                Decode12BitRawBEInterlaced(s, w, h);
            }
            else
            {
                throw new RawDecoderException("ORF Decoder: Don't know how to handle the encoding in this file\n");
            }
        }

        /* This is probably the slowest decoder of them all.
         * I cannot see any way to effectively speed up the prediction
         * phase, which is by far the slowest part of this algorithm.
         * Also there is no way to multithread this code, since prediction
         * is based on the output of all previous pixel (bar the first four)
         */
        private unsafe void DecodeCompressed(TIFFBinaryReader s, Int32 w, Int32 h)
        {
            int nbits, sign, low, high, left0, nw0, left1, nw1, i;
            long[] acarry0 = new long[3], acarry1 = new long[3];
            int pred, diff;

            //uint pitch = rawImage.pitch;

            /* Build a table to quickly look up "high" value */
            byte[] bittable = new byte[4096];
            for (i = 0; i < 4096; i++)
            {
                int b = i;
                for (high = 0; high < 12; high++)
                    if (((b >> (11 - high)) & 1) != 0)
                        break;
                bittable[i] = (byte)Math.Min(12, high);
            }
            left0 = nw0 = left1 = nw1 = 0;
            s.ReadBytes(7);
            BitPumpMSB bits = new BitPumpMSB(ref s);

            for (int y = 0; y < h; y++)
            {
                fixed (UInt16* dest = &rawImage.rawData[y * rawImage.dim.width])
                {
                    bool y_border = y < 2;
                    bool border = true;
                    for (int x = 0; x < w; x++)
                    {
                        bits.CheckPos();
                        bits.FillCheck();

                        if (acarry0[2] < 3) i = 2;
                        else i = 0;

                        for (nbits = 2 + i; (UInt16)acarry0[0] >> (nbits + i) != 0; nbits++) ;

                        uint b = bits.PeekBitsNoFill(15);
                        sign = (int)(b >> 14) * -1;
                        low = (int)(b >> 12) & 3;
                        high = bittable[b & 4095];

                        // Skip bytes used above or read bits
                        if (high == 12)
                        {
                            bits.SkipBitsNoFill(15);
                            high = (int)bits.GetBits((uint)(16 - nbits)) >> 1;
                        }
                        else
                        {
                            bits.SkipBitsNoFill((uint)high + 1 + 3);
                        }

                        acarry0[0] = (uint)(high << nbits) | bits.GetBits((uint)nbits);
                        diff = (int)((acarry0[0] ^ sign) + acarry0[1]);
                        acarry0[1] = (diff * 3 + acarry0[1]) >> 5;
                        acarry0[2] = acarry0[0] > 16 ? 0 : acarry0[2] + 1;

                        if (border)
                        {
                            if (y_border && x < 2)
                                pred = 0;
                            else if (y_border)
                                pred = left0;
                            else
                            {
                                pred = dest[-rawImage.dim.width + x];
                                nw0 = pred;
                            }
                            dest[x] = (ushort)(pred + ((diff << 2) | low));
                            // Set predictor
                            left0 = dest[x];
                        }
                        else
                        {
                            // Have local variables for values used several tiles
                            // (having a "UInt16 *dst_up" that caches dest[-pitch+((int)x)] is actually slower, probably stack spill or aliasing)
                            int up = dest[-rawImage.dim.width + x];
                            int leftMinusNw = left0 - nw0;
                            int upMinusNw = up - nw0;
                            // Check if sign is different, and one is not zero
                            if (leftMinusNw * upMinusNw < 0)
                            {
                                if (Other_abs(leftMinusNw) > 32 || Other_abs(upMinusNw) > 32)

                                    pred = left0 + upMinusNw;
                                else
                                    pred = (left0 + up) >> 1;
                            }
                            else
                                pred = Other_abs(leftMinusNw) > Other_abs(upMinusNw) ? left0 : up;

                            dest[x] = (ushort)(pred + ((diff << 2) | low));
                            // Set predictors
                            left0 = dest[x];
                            nw0 = up;
                        }

                        // ODD PIXELS
                        x += 1;
                        bits.FillCheck();
                        if (acarry1[2] < 3) i = 2;
                        else i = 0;
                        for (nbits = 2 + i; (UInt16)acarry1[0] >> (nbits + i) != 0; nbits++) ;
                        b = bits.PeekBitsNoFill(15);
                        sign = (int)(b >> 14) * -1;
                        low = (int)(b >> 12) & 3;
                        high = bittable[b & 4095];

                        // Skip bytes used above or read bits
                        if (high == 12)
                        {
                            bits.SkipBitsNoFill(15);
                            high = (int)bits.GetBits((uint)(16 - nbits)) >> 1;
                        }
                        else
                        {
                            bits.SkipBitsNoFill((uint)high + 1 + 3);
                        }

                        acarry1[0] = (uint)(high << nbits) | bits.GetBits((uint)nbits);
                        diff = (int)((acarry1[0] ^ sign) + acarry1[1]);
                        acarry1[1] = (diff * 3 + acarry1[1]) >> 5;
                        acarry1[2] = acarry1[0] > 16 ? 0 : acarry1[2] + 1;

                        if (border)
                        {
                            if (y_border && x < 2)
                                pred = 0;
                            else if (y_border)
                                pred = left1;
                            else
                            {
                                pred = dest[-rawImage.dim.width + x];
                                nw1 = pred;
                            }
                            dest[x] = (ushort)(left1 = pred + ((diff << 2) | low));
                        }
                        else
                        {
                            int up = dest[-rawImage.dim.width + x];
                            int leftminusNw = left1 - nw1;
                            int upminusNw = up - nw1;

                            // Check if sign is different, and one is not zero
                            if (leftminusNw * upminusNw < 0)
                            {
                                if (Other_abs(leftminusNw) > 32 || Other_abs(upminusNw) > 32)

                                    pred = left1 + upminusNw;
                                else
                                    pred = (left1 + up) >> 1;
                            }
                            else
                                pred = Other_abs(leftminusNw) > Other_abs(upminusNw) ? left1 : up;

                            dest[x] = (ushort)(left1 = pred + ((diff << 2) | low));
                            nw1 = up;
                        }
                        border = y_border;
                    }
                }
            }
        }

        public override void DecodeMetadata()
        {
            int iso = 0;
            rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            List<IFD> data = ifd.GetIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("ORF Meta Decoder: Model name found");

            string make = data[0].GetEntry(TagType.MAKE).DataAsString;
            string model = data[0].GetEntry(TagType.MODEL).DataAsString;

            var isoTag = ifd.GetEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                iso = isoTag.GetInt(0);

            SetMetaData(model);

            var rMul = ifd.GetEntryRecursive(TagType.OLYMPUSREDMULTIPLIER);
            var bMul = ifd.GetEntryRecursive(TagType.OLYMPUSBLUEMULTIPLIER);
            if (rMul != null && bMul != null)
            {
                rawImage.metadata.wbCoeffs[0] = ifd.GetEntryRecursive(TagType.OLYMPUSREDMULTIPLIER).GetShort(0);
                rawImage.metadata.wbCoeffs[1] = 256.0f;
                rawImage.metadata.wbCoeffs[2] = ifd.GetEntryRecursive(TagType.OLYMPUSBLUEMULTIPLIER).GetShort(0);
            }
            else
            {
                // Newer cameras process the Image Processing SubIFD in the makernote
                Tag img_entry = ifd.GetEntryRecursive(TagType.OLYMPUSIMAGEPROCESSING);
                if (img_entry != null)
                {
                    uint offset = (uint)(img_entry.GetUInt(0) + img_entry.parent_offset - 12);
                    try
                    {
                        IFD image_processing = new IFD(reader, offset, ifd.endian);
                        Tag wb = image_processing.GetEntry((TagType)0x0100);
                        // Get the WB
                        if (wb != null)
                        {
                            if (wb.dataCount == 4)
                            {
                                wb.parent_offset = img_entry.parent_offset - 12;
                                // wb.offsetFromParent();
                            }
                            if (wb.dataCount == 2 || wb.dataCount == 4)
                            {
                                rawImage.metadata.wbCoeffs[0] = wb.GetFloat(0);
                                rawImage.metadata.wbCoeffs[1] = 256.0f;
                                rawImage.metadata.wbCoeffs[2] = wb.GetFloat(1);
                            }
                        }


                        Tag blackEntry = image_processing.GetEntry((TagType)0x0600);
                        // Get the black levels
                        if (blackEntry != null)
                        {
                            // Order is assumed to be RGGB
                            if (blackEntry.dataCount == 4)
                            {
                                //blackEntry.parent_offset = img_entry.parent_offset - 12;
                                //blackEntry.offsetFromParent();
                                for (int i = 0; i < 4; i++)
                                {
                                    if (rawImage.cfa.cfa[(i & 1) * 2 + i >> 1] == CFAColor.RED)
                                        rawImage.blackLevelSeparate[i] = blackEntry.GetShort(0);
                                    else if (rawImage.cfa.cfa[(i & 1) * 2 + i >> 1] == CFAColor.BLUE)
                                        rawImage.blackLevelSeparate[i] = blackEntry.GetShort(3);
                                    else if (rawImage.cfa.cfa[(i & 1) * 2 + i >> 1] == CFAColor.GREEN && i < 2)
                                        rawImage.blackLevelSeparate[i] = blackEntry.GetShort(1);
                                    else if (rawImage.cfa.cfa[(i & 1) * 2 + i >> 1] == CFAColor.GREEN)
                                        rawImage.blackLevelSeparate[i] = blackEntry.GetShort(2);
                                }
                                // Adjust whitelevel based on the read black (we assume the dynamic range is the same)
                                rawImage.whitePoint -= (uint)(rawImage.blackLevel - rawImage.blackLevelSeparate[0]);
                            }
                        }
                    }
                    catch (RawDecoderException e)
                    {
                        rawImage.errors.Add(e.Message);
                    }

                }
            }
        }

        private void SetMetaData(string model)
        {
        }
    }
}

