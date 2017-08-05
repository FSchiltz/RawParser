using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet.Decoder
{
    class ORFDecoder : TIFFDecoder
    {
        internal ORFDecoder(Stream file) : base(file) { }

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

            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

            rawImage.fullSize.dim = new Point2D(width, height);
            rawImage.Init(false);

            // We add 3 bytes slack, since the bitpump might be a few bytes ahead.
            ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, off);
            input.BaseStream.Position = off;
            try
            {
                if (offsets.dataCount != 1)
                    DecodeUncompressed(input, width, height, size, raw.endian);
                else
                    DecodeCompressed(input, width, height);
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
            }
        }

        private void DecodeUncompressed(ImageBinaryReader input, uint width, uint height, long size, Endianness endian)
        {
            /*
                 RawDecompressor.Decode12BitRawWithControl(s, w, h, rawImage);
             else if ((hints.ContainsKey("jpeg32_bitorder")))
             {
                 Point2D dim = new Point2D(w, h), pos = new Point2D(0, 0);
                 RawDecompressor.ReadUncompressedRaw(s, dim, pos, w * 12 / 8, 12, BitOrder.Jpeg32, rawImage);
             }
             else*/
            if (size >= width * height * 2)
            { // We're in an unpacked raw
                if (endian == Endianness.Little)
                    RawDecompressor.Decode12BitRawUnpacked(input, new Point2D(width, height), new Point2D(), rawImage);
                else
                    RawDecompressor.Decode12BitRawBEunpackedLeftAligned(input, new Point2D(width, height), new Point2D(), rawImage);
            }
            else if (size >= width * height * 3 / 2)
            { // We're in one of those weird interlaced packed raws
                RawDecompressor.Decode12BitRawBEInterlaced(input, new Point2D(width, height), new Point2D(), rawImage);
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
        private void DecodeCompressed(ImageBinaryReader s, uint width, uint height)
        {
            int nbits;
            long left0, nw0, left1, nw1;
            long sign, low, high;
            long[] acarry0 = new long[3], acarry1 = new long[3];
            long pred, diff;

            //uint pitch = rawImage.pitch;

            /* Build a table to quickly look up "high" value */
            byte[] bittable = new byte[4096];
            for (int i = 0; i < 4096; i++)
            {
                int b = i;
                for (high = 0; high < 12; high++)
                    if (((b >> (11 - (int)high)) & 1) != 0)
                        break;
                bittable[i] = (byte)Math.Min(12, high);
            }
            left0 = nw0 = left1 = nw1 = 0;
            s.ReadBytes(7);
            BitPumpMSB bits = new BitPumpMSB(s);

            for (int y = 0; y < height; y++)
            {
                var pos = y * rawImage.fullSize.UncroppedDim.width;
                acarry0 = new long[3];
                acarry1 = new long[3];
                bool y_border = y < 2;
                bool border = true;
                for (int x = 0; x < width; x++)
                {
                    bits.Fill();
                    int i = 0;
                    if (acarry0[2] < 3) i = 2;

                    for (nbits = 2 + i; acarry0[0] >> (nbits + i) != 0; nbits++) ;

                    uint b = bits.PeekBits(15);
                    sign = (b >> 14) * -1;
                    low = (b >> 12) & 3;
                    high = bittable[b & 4095];

                    // Skip bytes used above or read bits
                    if (high == 12)
                    {
                        bits.SkipBits(15);
                        high = bits.GetBits(16 - nbits) >> 1;
                    }
                    else
                    {
                        bits.SkipBits((int)high + 1 + 3);
                    }

                    acarry0[0] = (high << nbits) | bits.GetBits(nbits);
                    diff = (acarry0[0] ^ sign) + acarry0[1];
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
                            pred = nw0 = rawImage.fullSize.rawView[pos - rawImage.fullSize.UncroppedDim.width + x];
                        }
                        rawImage.fullSize.rawView[pos + x] = (ushort)(pred + ((diff << 2) | low));
                        // Set predictor
                        left0 = rawImage.fullSize.rawView[pos + x];
                    }
                    else
                    {
                        // Have local variables for values used several tiles
                        // (having a "UInt16 *dst_up" that caches dest[-pitch+((int)x)] is actually slower, probably stack spill or aliasing)
                        int up = rawImage.fullSize.rawView[pos - rawImage.fullSize.UncroppedDim.width + x];
                        long leftMinusNw = left0 - nw0;
                        long upMinusNw = up - nw0;
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

                        rawImage.fullSize.rawView[pos + x] = (ushort)(pred + ((diff << 2) | low));
                        // Set predictors
                        left0 = rawImage.fullSize.rawView[pos + x];
                        nw0 = up;
                    }

                    // ODD PIXELS
                    x += 1;
                    bits.Fill();
                    i = 0;
                    if (acarry1[2] < 3) i = 2;

                    for (nbits = 2 + i; acarry1[0] >> (nbits + i) != 0; nbits++) ;
                    b = bits.PeekBits(15);
                    sign = (b >> 14) * -1;
                    low = (b >> 12) & 3;
                    high = bittable[b & 4095];

                    // Skip bytes used above or read bits
                    if (high == 12)
                    {
                        bits.SkipBits(15);
                        high = bits.GetBits(16 - nbits) >> 1;
                    }
                    else
                    {
                        bits.SkipBits((int)high + 1 + 3);
                    }

                    acarry1[0] = (high << nbits) | bits.GetBits(nbits);
                    diff = (acarry1[0] ^ sign) + acarry1[1];
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
                            pred = nw1 = rawImage.fullSize.rawView[pos - rawImage.fullSize.UncroppedDim.width + x];
                        }
                        rawImage.fullSize.rawView[pos + x] = (ushort)(left1 = pred + ((diff << 2) | low));
                    }
                    else
                    {
                        int up = rawImage.fullSize.rawView[pos - rawImage.fullSize.UncroppedDim.width + x];
                        long leftminusNw = left1 - nw1;
                        long upminusNw = up - nw1;

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

                        rawImage.fullSize.rawView[pos + x] = (ushort)(left1 = pred + ((diff << 2) | low));
                        nw1 = up;
                    }
                    border = y_border;
                }
            }
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();
            if (rawImage.metadata.Model == null)
                throw new RawDecoderException("ORF Meta Decoder: Model name found");
            SetMetaData(rawImage.metadata.Model);

            rawImage.metadata.Lens = ifd.GetEntryRecursive((TagType)42036)?.DataAsString;

            var rMul = ifd.GetEntryRecursive(TagType.OLYMPUSREDMULTIPLIER);
            var bMul = ifd.GetEntryRecursive(TagType.OLYMPUSBLUEMULTIPLIER);
            if (rMul != null && bMul != null)
            {
                rawImage.metadata.WbCoeffs = new WhiteBalance(
                    ifd.GetEntryRecursive(TagType.OLYMPUSREDMULTIPLIER).GetShort(0),
                    1,
                    ifd.GetEntryRecursive(TagType.OLYMPUSREDMULTIPLIER).GetShort(0));
            }
            else
            {
                IFD image_processing = ifd.GetIFDWithType(IFDType.Makernote).subIFD[0];
                Tag wb = image_processing.GetEntry((TagType)0x0100);
                // Get the WB
                if (wb?.dataCount == 2 || wb?.dataCount == 4)
                {
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(0), 256, wb.GetInt(1), rawImage.fullSize.ColorDepth);
                }

                //TODO fix (the sub makernote doesn't read the correct value
                rawImage.metadata.WbCoeffs = new WhiteBalance(1, 1, 1);

                Tag blackEntry = image_processing.GetEntry((TagType)0x0600);
                // Get the black levels
                if (blackEntry != null)
                {
                    Debug.Assert(blackEntry.GetInt(0) == blackEntry.GetInt(1));
                    rawImage.black = blackEntry.GetInt(0);
                    // Order is assumed to be RGGB
                    if (blackEntry.dataCount == 4)
                    {
                        //blackEntry.parent_offset = img_entry.parent_offset - 12;
                        //blackEntry.offsetFromParent();
                        /*for (int i = 0; i < 4; i++)
                        {
                            if (rawImage.colorFilter.cfa[(i & 1) * 2 + i >> 1] == CFAColor.Red)
                                rawImage.blackLevelSeparate[i] = blackEntry.GetShort(0);
                            else if (rawImage.colorFilter.cfa[(i & 1) * 2 + i >> 1] == CFAColor.Blue)
                                rawImage.blackLevelSeparate[i] = blackEntry.GetShort(3);
                            else if (rawImage.colorFilter.cfa[(i & 1) * 2 + i >> 1] == CFAColor.Green && i < 2)
                                rawImage.blackLevelSeparate[i] = blackEntry.GetShort(1);
                            else if (rawImage.colorFilter.cfa[(i & 1) * 2 + i >> 1] == CFAColor.Green)
                                rawImage.blackLevelSeparate[i] = blackEntry.GetShort(2);
                        }*/
                        // Adjust whitelevel based on the read black (we assume the dynamic range is the same)
                        //rawImage.whitePoint -= rawImage.black - rawImage.bla[0];
                    }
                }
            }
        }

        private void SetMetaData(string model)
        {
            //find the color matrice
            for (int i = 0; i < colorM.Length; i++)
            {
                if (colorM[i].name.Contains(rawImage.metadata.Model))
                {
                    rawImage.convertionM = colorM[i].matrix;
                    if (colorM[i].black != 0) rawImage.black = colorM[i].black;
                    if (colorM[i].white != 0) rawImage.whitePoint = colorM[i].white;
                    break;
                }
            }
        }

        private CamRGB[] colorM = {
            /*{ "Olympus AIR A01", 0, 0,    { 8992,-3093,-639,-2563,10721,2122,-437,1270,5473 }),
    { "Olympus C5050", 0, 0,    { 10508,-3124,-1273,-6079,14294,1901,-1653,2306,6237 }),
    { "Olympus C5060", 0, 0,    { 10445,-3362,-1307,-7662,15690,2058,-1135,1176,7602 }),
    { "Olympus C7070", 0, 0,    { 10252,-3531,-1095,-7114,14850,2436,-1451,1723,6365 }),
    { "Olympus C70", 0, 0,    { 10793,-3791,-1146,-7498,15177,2488,-1390,1577,7321 }),
    { "Olympus C80", 0, 0,    { 8606,-2509,-1014,-8238,15714,2703,-942,979,7760 }),*/
     new CamRGB("Olympus E-10", 0, 0xffc,  new double[]  { 12745,-4500,-1416,-6062,14542,1580,-1934,2256,6603 } ),
    new CamRGB( "Olympus E-1", 0, 0,   new double[] { 11846,-4767,-945,-7027,15878,1089,-2699,4122,8311 }),
     new CamRGB("Olympus E-20", 0, 0xffc,   new double[] { 13173,-4732,-1499,-5807,14036,1895,-2045,2452,7142 } ),
     new CamRGB("Olympus E-300", 0, 0,   new double[] { 7828,-1761,-348,-5788,14071,1830,-2853,4518,6557 } ),
  new CamRGB(  "Olympus E-330", 0, 0,    new double[]{ 8961,-2473,-1084,-7979,15990,2067,-2319,3035,8249 } ),
    new CamRGB( "Olympus E-30", 0, 0xfbc,   new double[] { 8144,-1861,-1111,-7763,15894,1929,-1865,2542,7607 }),
 new CamRGB(    "Olympus E-3", 0, 0xf99,   new double[] { 9487,-2875,-1115,-7533,15606,2010,-1618,2100,7389 }),
     new CamRGB("Olympus E-400", 0, 0,   new double[] { 6169,-1483,-21,-7107,14761,2536,-2904,3580,8568 }),
    new CamRGB( "Olympus E-410", 0, 0xf6a,    new double[]{ 8856,-2582,-1026,-7761,15766,2082,-2009,2575,7469 }),
    new CamRGB("Olympus E-420", 0, 0xfd7,   new double[] { 8746,-2425,-1095,-7594,15612,2073,-1780,2309,7416 }),
    new CamRGB( "Olympus E-450", 0, 0xfd2,  new double[]  { 8745,-2425,-1095,-7594,15613,2073,-1780,2309,7416 }),
   new CamRGB(  "Olympus E-500", 0, 0,  new double[]  { 8136,-1968,-299,-5481,13742,1871,-2556,4205,6630 }),
  new CamRGB(   "Olympus E-510", 0, 0xf6a, new double[]   { 8785,-2529,-1033,-7639,15624,2112,-1783,2300,7817 }),
  new CamRGB(   "Olympus E-520", 0, 0xfd2,  new double[]  { 8344,-2322,-1020,-7596,15635,2048,-1748,2269,7287 }),
  new CamRGB(   "Olympus E-5", 0, 0xeec,   new double[] { 11200,-3783,-1325,-4576,12593,2206,-695,1742,7504 }),
  new CamRGB(  "Olympus E-600", 0, 0xfaf, new double[]   { 8453,-2198,-1092,-7609,15681,2008,-1725,2337,7824 }),
  new CamRGB(   "Olympus E-620", 0, 0xfaf,  new double[]  { 8453,-2198,-1092,-7609,15681,2008,-1725,2337,7824 }),
  new CamRGB(   "Olympus E-P1", 0, 0xffd,   new double[] { 8343,-2050,-1021,-7715,15705,2103,-1831,2380,8235 }),
  new CamRGB(   "Olympus E-P2", 0, 0xffd,  new double[]  { 8343,-2050,-1021,-7715,15705,2103,-1831,2380,8235 }),/*
    { "Olympus E-P3", 0, 0,    { 7575,-2159,-571,-3722,11341,2725,-1434,2819,6271 }),
    { "Olympus E-P5", 0, 0,    { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),
    { "Olympus E-PL1s", 0, 0,    { 11409,-3872,-1393,-4572,12757,2003,-709,1810,7415 }),
    { "Olympus E-PL1", 0, 0,    { 11408,-4289,-1215,-4286,12385,2118,-387,1467,7787 }),*/
    new CamRGB( "Olympus E-PL2", 0, 0xcf3,  new double[]  { 15030,-5552,-1806,-3987,12387,1767,-592,1670,7023 }),
     new CamRGB("Olympus E-PL3", 0, 0,  new double[]  { 7575,-2159,-571,-3722,11341,2725,-1434,2819,6271 }),
new CamRGB(    "Olympus E-PL5", 0, 0xfcb,  new double[]  { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),/*
    { "Olympus E-PL6", 0, 0,    { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),
    { "Olympus E-PL7", 0, 0,    { 9197,-3190,-659,-2606,10830,2039,-458,1250,5458 }),
    { "Olympus E-PM1", 0, 0,    { 7575,-2159,-571,-3722,11341,2725,-1434,2819,6271 }),
    { "Olympus E-PM2", 0, 0,    { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),
    { "Olympus E-M10", 0, 0,// also E-M10 Mark II 
    { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),
    { "Olympus E-M1", 0, 0,    { 7687,-1984,-606,-4327,11928,2721,-1381,2339,6452 }),
    { "Olympus E-M5MarkII", 0, 0,    { 9422,-3258,-711,-2655,10898,2015,-512,1354,5512 }),*/
  new CamRGB(   "Olympus E-M5", 0, 0xfe1,  new double[]  { 8380,-2630,-639,-2887,10725,2496,-627,1427,5438 }),
    /*
    { "Olympus PEN-F", 0, 0,    { 9476,-3182,-765,-2613,10958,1893,-449,1315,5268 }),
    { "Olympus SH-2", 0, 0,    { 10156,-3425,-1077,-2611,11177,1624,-385,1592,5080 }),
    { "Olympus SP350", 0, 0,    { 12078,-4836,-1069,-6671,14306,2578,-786,939,7418 }),
    { "Olympus SP3", 0, 0,    { 11766,-4445,-1067,-6901,14421,2707,-1029,1217,7572 }),*/
   new CamRGB(  "Olympus SP500UZ", 0, 0xfff,  new double[]  { 9493,-3415,-666,-5211,12334,3260,-1548,2262,6482 }),
   new CamRGB(  "Olympus SP510UZ", 0, 0xffe,  new double[]  { 10593,-3607,-1010,-5881,13127,3084,-1200,1805,6721 }),
   new CamRGB(  "Olympus SP550UZ", 0, 0xffe,   new double[] { 11597,-4006,-1049,-5432,12799,2957,-1029,1750,6516 }),
    new CamRGB( "Olympus SP560UZ", 0, 0xff9,   new double[] { 10915,-3677,-982,-5587,12986,2911,-1168,1968,6223 }),/*
    { "Olympus SP570UZ", 0, 0,    { 11522,-4044,-1146,-4736,12172,2904,-988,1829,6039 }),
    { "Olympus STYLUS1", 0, 0,    { 8360,-2420,-880,-3928,12353,1739,-1381,2416,5173 }),
    { "Olympus TG-4", 0, 0,    { 11426,-4159,-1126,-2066,10678,1593,-120,1327,4998 }),
    { "Olympus XZ-10", 0, 0,    { 9777,-3483,-925,-2886,11297,1800,-602,1663,5134 }),
    { "Olympus XZ-1", 0, 0,    { 10901,-4095,-1074,-1141,9208,2293,-62,1417,5158 }),
    { "Olympus XZ-2", 0, 0,    { 9777,-3483,-925,-2886,11297,1800,-602,1663,5134 }),};*/
        };
    }
}

