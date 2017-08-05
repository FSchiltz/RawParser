using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet.Decoder
{
    class RW2Decoder : TIFFDecoder
    {
        UInt32 load_flags;
        ImageBinaryReader input_start;
        IFD raw;

        public override Thumbnail DecodeThumb()
        {
            var thumb = base.DecodeThumb();
            if (thumb == null)
            {
                var jpegTag = ifd.GetEntryRecursive(TagType.MAKERNOTE_ALT);
                if (jpegTag == null) return null;
                reader.BaseStream.Position = jpegTag.dataOffset;
                return new JPEGThumbnail(reader.ReadBytes((int)jpegTag.dataCount));
            }
            else return thumb;
        }

        internal RW2Decoder(Stream reader) : base(reader) { }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.PANASONIC_STRIPOFFSET);

            bool isOldPanasonic = false;

            if (data.Count == 0)
            {
                data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);
                if (data == null)
                    throw new RawDecoderException("No image data found");
                isOldPanasonic = true;
            }

            raw = data[0];
            uint height = raw.GetEntry((TagType)3).GetUInt(0);
            uint width = raw.GetEntry((TagType)2).GetUInt(0);

            if (isOldPanasonic)
            {
                Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);

                if (offsets.dataCount != 1)
                {
                    throw new RawDecoderException("Multiple Strips found:" + offsets.dataCount);
                }
                uint off = offsets.GetUInt(0);
                if (!reader.IsValid(off))
                    throw new RawDecoderException("Invalid image data offset, cannot decode.");

                rawImage.fullSize.dim = new Point2D(width, height);
                rawImage.Init(false);

                UInt32 size = (uint)(reader.BaseStream.Length - off);
                input_start = new ImageBinaryReader(stream, off);

                if (size >= width * height * 2)
                {
                    // It's completely unpacked little-endian
                    RawDecompressor.Decode12BitRawUnpacked(input_start, new Point2D(width, height), new Point2D(), rawImage);
                    rawImage.fullSize.ColorDepth = 12;
                }
                else if (size >= width * height * 3 / 2)
                {
                    // It's a packed format
                    RawDecompressor.Decode12BitRawWithControl(input_start, new Point2D(width, height), new Point2D(), rawImage);
                    rawImage.fullSize.ColorDepth = 12;
                }
                else
                {
                    var colorTag = raw.GetEntry((TagType)5);
                    if (colorTag != null)
                    {
                        rawImage.fullSize.ColorDepth = colorTag.GetUShort(0);
                    }
                    else
                    {
                        //try to load with 12bits colordepth
                    }
                    // It's using the new .RW2 decoding method
                    load_flags = 0;
                    DecodeRw2();
                }
            }
            else
            {
                rawImage.fullSize.dim = new Point2D(width, height);
                rawImage.Init(false);
                Tag offsets = raw.GetEntry(TagType.PANASONIC_STRIPOFFSET);

                if (offsets.dataCount != 1)
                {
                    throw new RawDecoderException("Multiple Strips found:" + offsets.dataCount);
                }

                load_flags = 0x2008;
                uint off = offsets.GetUInt(0);

                if (!reader.IsValid(off))
                    throw new RawDecoderException("Invalid image data offset, cannot decode.");

                input_start = new ImageBinaryReader(stream, off);
                DecodeRw2();
            }
        }

        unsafe void DecodeRw2()
        {
            int i, j, sh = 0;
            int[] pred = new int[2], nonz = new int[2];
            uint w = rawImage.fullSize.dim.width / 14;
            bool zero_is_bad = true;

            PanaBitpump bits = new PanaBitpump(input_start, load_flags);
            List<Int32> zero_pos = new List<int>();
            for (int y = 0; y < rawImage.fullSize.dim.height; y++)
            {
                for (int x = 0, dest = 0; x < w; x++)
                {
                    pred[0] = pred[1] = nonz[0] = nonz[1] = 0;
                    int u = 0;
                    for (i = 0; i < 14; i++)
                    {
                        // Even pixels
                        if (u == 2)
                        {
                            sh = 4 >> 3 - bits.GetBits(2);
                            u = -1;
                        }
                        if (nonz[0] != 0)
                        {
                            if (0 != (j = bits.GetBits(8)))
                            {
                                if ((pred[0] -= 0x80 << sh) < 0 || sh == 4)
                                    pred[0] &= ~(-1 << sh);
                                pred[0] += j << sh;
                            }
                        }
                        else if ((nonz[0] = bits.GetBits(8)) != 0 || i > 11)
                            pred[0] = nonz[0] << 4 | bits.GetBits(4);
                        rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width + (dest++)] = (ushort)pred[0];

                        if (zero_is_bad && 0 == pred[0])
                            zero_pos.Add((y << 16) | (x * 14 + i));

                        // Odd pixels
                        i++;
                        u++;
                        if (u == 2)
                        {
                            sh = 4 >> 3 - bits.GetBits(2);
                            u = -1;
                        }
                        if (nonz[1] != 0)
                        {
                            if ((j = bits.GetBits(8)) != 0)
                            {
                                if ((pred[1] -= 0x80 << sh) < 0 || sh == 4)
                                    pred[1] &= ~(-1 << sh);
                                pred[1] += j << sh;
                            }
                        }
                        else if ((nonz[1] = bits.GetBits(8)) != 0 || i > 11)
                            pred[1] = nonz[1] << 4 | bits.GetBits(4);
                        rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width + (dest++)] = (ushort)pred[1];

                        if (zero_is_bad && 0 == pred[1])
                            zero_pos.Add((y << 16) | (x * 14 + i));
                        u++;
                    }
                }
            }
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();

            if (rawImage.metadata.Model == null)
                throw new RawDecoderException("Model name not found");
            if (rawImage.metadata.Make == null)
                throw new RawDecoderException("Make name not found");

            string mode = GuessMode();

            SetMetadata(rawImage.metadata.Model);
            rawImage.metadata.Mode = mode;

            //in panasonic, exif are in ifd 0
            if (rawImage.fullSize.ColorDepth == 16)
            {
                rawImage.fullSize.ColorDepth = 12;
            }

            //panasonic iso is in a special tag
            if (rawImage.metadata.IsoSpeed == 0)
            {
                var t = raw.GetEntryRecursive(TagType.PANASONIC_ISO_SPEED);
                if (t != null) rawImage.metadata.IsoSpeed = t.GetInt(0);
            }

            // Read blacklevels
            var bias = raw.GetEntry((TagType)0x08).GetInt(0) + raw.GetEntry((TagType)0x09).GetInt(0) + raw.GetEntry((TagType)0x0a).GetInt(0);

            var rTag = raw.GetEntry((TagType)0x1c);
            var gTag = raw.GetEntry((TagType)0x1d);
            var bTag = raw.GetEntry((TagType)0x1e);
            if (rTag != null && gTag != null && bTag != null)
            {
                Debug.Assert(bTag.GetInt(0) + 15 == rTag.GetInt(0) + 15);
                Debug.Assert(bTag.GetInt(0) + 15 == gTag.GetInt(0) + 15);
                rawImage.black = rTag.GetInt(0) + bias;
                /*
                rawImage.blackLevelSeparate[0] = rTag.GetInt(0) + 15;
                rawImage.blackLevelSeparate[1] = rawImage.blackLevelSeparate[2] = gTag.GetInt(0) + 15;
                rawImage.blackLevelSeparate[3] = bTag.GetInt(0) + 15;*/
            }

            // Read WB levels
            var rWBTag = raw.GetEntry((TagType)0x0024);
            var gWBTag = raw.GetEntry((TagType)0x0025);
            var bWBTag = raw.GetEntry((TagType)0x0026);
            if (rWBTag != null && gWBTag != null && bWBTag != null)
            {
                rawImage.metadata.WbCoeffs = new WhiteBalance(bWBTag.GetShort(0), gWBTag.GetShort(0), rWBTag.GetShort(0), rawImage.fullSize.ColorDepth);
            }
            else
            {
                var wb1Tag = raw.GetEntry((TagType)0x0011);
                var wb2Tag = raw.GetEntry((TagType)0x0012);
                if (wb1Tag != null && wb2Tag != null)
                {
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb1Tag.GetShort(0), 1, wb2Tag.GetShort(0));
                }
            }
        }

        private void SetMetadata(string model)
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
            switch (model)
            {
                case "":
                    rawImage.metadata.Model = "";
                    break;
            }
        }

        string GuessMode()
        {
            float ratio = 3.0f / 2.0f;  // Default

            if (rawImage.fullSize.rawView == null)
                return "";

            ratio = rawImage.fullSize.dim.width / (float)rawImage.fullSize.dim.height;

            float min_diff = Math.Abs(ratio - 16.0f / 9.0f);
            string closest_match = "16:9";

            float t = Math.Abs(ratio - 3.0f / 2.0f);
            if (t < min_diff)
            {
                closest_match = "3:2";
                min_diff = t;
            }

            t = Math.Abs(ratio - 4.0f / 3.0f);
            if (t < min_diff)
            {
                closest_match = "4:3";
                min_diff = t;
            }

            t = Math.Abs(ratio - 1.0f);
            if (t < min_diff)
            {
                closest_match = "1:1";
                min_diff = t;
            }
            return closest_match;
        }

        private CamRGB[] colorM = {
            new CamRGB("Panasonic DMC-CM1",15,0,    new double[]{ 8770,-3194,-820,-2871,11281,1803,-513,1552,4434 } ),
    new CamRGB("Panasonic DMC-FZ8", 0, 0xf7f,  new double[]  { 8986,-2755,-802,-6341,13575,3077,-1476,2144,6379 } ),
    new CamRGB( "Panasonic DMC-FZ18", 0, 0,  new double[]   { 9932,-3060,-935,-5809,13331,2753,-1267,2155,5575 }),
    new CamRGB( "Panasonic DMC-FZ28", 15, 0xf96,  new double[]   { 10109,-3488,-993,-5412,12812,2916,-1305,2140,5543 }),
    new CamRGB("Panasonic DMC-FZ330", 15, 0,  new double[]   { 8378,-2798,-769,-3068,11410,1877,-538,1792,4623 }),
     new CamRGB( "Panasonic DMC-FZ300", 15, 0,  new double[]   { 8378,-2798,-769,-3068,11410,1877,-538,1792,4623 }),
     new CamRGB( "Panasonic DMC-FZ30", 0, 0xf94,  new double[]   { 10976,-4029,-1141,-7918,15491,2600,-1670,2071,8246 }),
     new CamRGB( "Panasonic DMC-FZ3", 15, 0, new double[]    { 9938,-2780,-890,-4604,12393,2480,-1117,2304,4620 }),
     new CamRGB( "Panasonic DMC-FZ4", 15, 0,  new double[]   { 13639,-5535,-1371,-1698,9633,2430,316,1152,4108 }),
     new CamRGB( "Panasonic DMC-FZ50", 0, 0,  new double[]   { 7906,-2709,-594,-6231,13351,3220,-1922,2631,6537 }),
     new CamRGB( "Panasonic DMC-FZ7", 15, 0, new double[]    { 11532,-4324,-1066,-2375,10847,1749,-564,1699,4351 }),
     new CamRGB( "Leica V-LUX1", 0, 0, new double[]    { 7906,-2709,-594,-6231,13351,3220,-1922,2631,6537 }),
     new CamRGB( "Panasonic DMC-L10", 15, 0xf96,  new double[]   { 8025,-1942,-1050,-7920,15904,2100,-2456,3005,7039 }),
     new CamRGB( "Panasonic DMC-L1", 0, 0xf7f,  new double[]   { 8054,-1885,-1025,-8349,16367,2040,-2805,3542,7629 }),
     new CamRGB("Leica DIGILUX 3", 0, 0xf7f,   new double[]  { 8054,-1885,-1025,-8349,16367,2040,-2805,3542,7629 }),
     new CamRGB("Panasonic DMC-LC1", 0, 0,  new double[]   { 11340,-4069,-1275,-7555,15266,2448,-2960,3426,7685 }),
     new CamRGB("Leica DIGILUX 2", 0, 0,  new double[]   { 11340,-4069,-1275,-7555,15266,2448,-2960,3426,7685 }),
     new CamRGB("Panasonic DMC-LX100", 15, 0, new double[]    { 8844,-3538,-768,-3709,11762,2200,-698,1792,5220 }),
     new CamRGB("Leica D-LUX (Typ 109)", 15, 0, new double[]    { 8844,-3538,-768,-3709,11762,2200,-698,1792,5220 }),
     new CamRGB("Panasonic DMC-LF1", 15, 0,  new double[]   { 9379,-3267,-816,-3227,11560,1881,-926,1928,5340 }),
     new CamRGB("Leica C (Typ 112)", 15, 0,  new double[]   { 9379,-3267,-816,-3227,11560,1881,-926,1928,5340 }),
     new CamRGB("Panasonic DMC-LX1", 0, 0xf7f, new double[]    { 10704,-4187,-1230,-8314,15952,2501,-920,945,8927 }),
     new CamRGB("Leica D-LUX2", 0, 0xf7f,  new double[]   { 10704,-4187,-1230,-8314,15952,2501,-920,945,8927 }),
     new CamRGB("Panasonic DMC-LX2", 0, 0, new double[]    { 8048,-2810,-623,-6450,13519,3272,-1700,2146,7049 }),
     new CamRGB("Leica D-LUX3", 0, 0,  new double[]   { 8048,-2810,-623,-6450,13519,3272,-1700,2146,7049 }),
     new CamRGB("Panasonic DMC-LX3", 15, 0, new double[]    { 8128,-2668,-655,-6134,13307,3161,-1782,2568,6083 }),
     new CamRGB("Leica D-LUX 4", 15, 0,  new double[]   { 8128,-2668,-655,-6134,13307,3161,-1782,2568,6083 }),
     new CamRGB("Panasonic DMC-LX5", 15, 0,  new double[]   { 10909,-4295,-948,-1333,9306,2399,22,1738,4582 }),
     new CamRGB("Leica D-LUX 5", 15, 0,  new double[]   { 10909,-4295,-948,-1333,9306,2399,22,1738,4582 }),
     new CamRGB("Panasonic DMC-LX7", 15, 0, new double[]    { 10148,-3743,-991,-2837,11366,1659,-701,1893,4899 }),
     new CamRGB("Leica D-LUX 6", 15, 0,  new double[]   { 10148,-3743,-991,-2837,11366,1659,-701,1893,4899 }),
     new CamRGB("Panasonic DMC-FZ1000", 15, 0,  new double[]  { 7830,-2696,-763,-3325,11667,1866,-641,1712,4824 }),
     new CamRGB("Leica V-LUX (Typ 114)", 15, 0,   new double[]  { 7830,-2696,-763,-3325,11667,1866,-641,1712,4824 }),
     new CamRGB("Panasonic DMC-FZ100", 15, 0xfff,new double[]    { 16197,-6146,-1761,-2393,10765,1869,366,2238,5248 }),
     new CamRGB("Leica V-LUX 2", 15, 0xfff,new double[]    { 16197,-6146,-1761,-2393,10765,1869,366,2238,5248 }),
     new CamRGB("Panasonic DMC-FZ150", 15, 0xfff,new double[]    { 11904,-4541,-1189,-2355,10899,1662,-296,1586,4289 }),
     new CamRGB("Leica V-LUX 3", 15, 0xfff,new double[]    { 11904,-4541,-1189,-2355,10899,1662,-296,1586,4289 }),
     new CamRGB("Panasonic DMC-FZ200", 15, 0xfff,new double[]    { 8112,-2563,-740,-3730,11784,2197,-941,2075,4933 }),
     new CamRGB("Leica V-LUX 4", 15, 0xfff,new double[]    { 8112,-2563,-740,-3730,11784,2197,-941,2075,4933 }),
     new CamRGB("Panasonic DMC-FX150", 15, 0xfff,new double[]{ 9082,-2907,-925,-6119,13377,3058,-1797,2641,5609 }),
     new CamRGB("Panasonic DMC-G10", 0, 0,new double[]{ 10113,-3400,-1114,-4765,12683,2317,-377,1437,6710 }),
     new CamRGB("Panasonic DMC-G1", 15, 0xf94,new double[]    { 8199,-2065,-1056,-8124,16156,2033,-2458,3022,7220 }),
     new CamRGB("Panasonic DMC-G2", 15, 0xf3c,new double[]{ 10113,-3400,-1114,-4765,12683,2317,-377,1437,6710 }),
     new CamRGB("Panasonic DMC-G3", 15, 0xfff,new double[]    { 6763,-1919,-863,-3868,11515,2684,-1216,2387,5879 }),
     new CamRGB("Panasonic DMC-G5", 15, 0xfff,new double[]    { 7798,-2562,-740,-3879,11584,2613,-1055,2248,5434 }),
     new CamRGB("Panasonic DMC-G6", 15, 0xfff,new double[]    { 8294,-2891,-651,-3869,11590,2595,-1183,2267,5352 }),
     new CamRGB("Panasonic DMC-G7", 15, 0xfff,new double[]    { 7610,-2780,-576,-4614,12195,2733,-1375,2393,6490 }),
     new CamRGB("Panasonic DMC-GF1", 15, 0xf92,new double[]    { 7888,-1902,-1011,-8106,16085,2099,-2353,2866,7330 }),
     new CamRGB("Panasonic DMC-GF2", 15, 0xfff,new double[]    { 7888,-1902,-1011,-8106,16085,2099,-2353,2866,7330 }),
     new CamRGB("Panasonic DMC-GF3", 15, 0xfff,new double[]    { 9051,-2468,-1204,-5212,13276,2121,-1197,2510,6890 }),
     new CamRGB("Panasonic DMC-GF5", 15, 0xfff,new double[]    { 8228,-2945,-660,-3938,11792,2430,-1094,2278,5793 }),
     new CamRGB("Panasonic DMC-GF6", 15, 0,new double[]    { 8130,-2801,-946,-3520,11289,2552,-1314,2511,5791 }),
     new CamRGB("Panasonic DMC-GF7", 15, 0,new double[]    { 7610,-2780,-576,-4614,12195,2733,-1375,2393,6490 }),
     new CamRGB("Panasonic DMC-GF8", 15, 0,new double[]    { 7610,-2780,-576,-4614,12195,2733,-1375,2393,6490 }),
     new CamRGB("Panasonic DMC-GH1", 15, 0xf92,new double[]    { 6299,-1466,-532,-6535,13852,2969,-2331,3112,5984 }),
     new CamRGB("Panasonic DMC-GH2", 15, 0xf95,new double[]    { 7780,-2410,-806,-3913,11724,2484,-1018,2390,5298 }),
     new CamRGB("Panasonic DMC-GH3", 15, 0,new double[]    { 6559,-1752,-491,-3672,11407,2586,-962,1875,5130 }),
     new CamRGB("Panasonic DMC-GH4", 15, 0,new double[]    { 7122,-2108,-512,-3155,11201,2231,-541,1423,5045 }),
     new CamRGB("Panasonic DMC-GM1", 15, 0,new double[]    { 6770,-1895,-744,-5232,13145,2303,-1664,2691,5703 }),
     new CamRGB("Panasonic DMC-GM5", 15, 0,new double[]    { 8238,-3244,-679,-3921,11814,2384,-836,2022,5852 }),
     new CamRGB("Panasonic DMC-GX1", 15, 0,new double[]    { 6763,-1919,-863,-3868,11515,2684,-1216,2387,5879 }),
     new CamRGB("Panasonic DMC-GX7", 15, 0,new double[]    { 7610,-2780,-576,-4614,12195,2733,-1375,2393,6490 }),
     new CamRGB("Panasonic DMC-GX8", 15, 0,new double[]    { 7564,-2263,-606,-3148,11239,2177,-540,1435,4853 }),
     new CamRGB("Panasonic DMC-TZ1", 15, 0,new double[]    { 7790,-2736,-755,-3452,11870,1769,-628,1647,4898 }),
     new CamRGB("Panasonic DMC-ZS1", 15, 0,new double[]    { 7790,-2736,-755,-3452,11870,1769,-628,1647,4898 }),
     new CamRGB("Panasonic DMC-TZ6", 15, 0,new double[]    { 8607,-2822,-808,-3755,11930,2049,-820,2060,5224 }),
     new CamRGB("Panasonic DMC-ZS4", 15, 0,new double[]    { 8607,-2822,-808,-3755,11930,2049,-820,2060,5224 }),
     new CamRGB("Panasonic DMC-TZ7", 15, 0,new double[]    { 8802,-3135,-789,-3151,11468,1904,-550,1745,4810 }),
     new CamRGB("Panasonic DMC-ZS5", 15, 0,new double[]    { 8802,-3135,-789,-3151,11468,1904,-550,1745,4810 }),
     new CamRGB("Panasonic DMC-TZ8", 15, 0,new double[]    { 8550,-2908,-842,-3195,11529,1881,-338,1603,4631 }),
     new CamRGB("Panasonic DMC-ZS6", 15, 0,new double[]    { 8550,-2908,-842,-3195,11529,1881,-338,1603,4631 }),
     new CamRGB("Leica S (Typ 007)", 0, 0,new double[]    { 6063,-2234,-231,-5210,13787,1500,-1043,2866,6997 }),
     new CamRGB("Leica X", 0, 0,    new double[]    { 7712,-2059,-653,-3882,11494,2726,-710,1332,5958 }),
     new CamRGB("Leica Q (Typ 116)", 0, 0,new double[]    { 11865,-4523,-1441,-5423,14458,935,-1587,2687,4830 }),
     new CamRGB("Leica M (Typ 262)", 0, 0,new double[]    { 6653,-1486,-611,-4221,13303,929,-881,2416,7226 }),
     new CamRGB("Leica SL (Typ 601)", 0, 0,new double[]     { 11865,-4523,-1441,-5423,14458,935,-1587,2687,4830}    )};
    }
}