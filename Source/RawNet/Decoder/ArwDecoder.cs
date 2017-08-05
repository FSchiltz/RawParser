using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet.Decoder
{
    class ArwDecoder : TIFFDecoder
    {
        internal ArwDecoder(Stream file) : base(file) { }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
            {
                Tag model = ifd.GetEntryRecursive(TagType.MODEL);
                if (model != null && model.DataAsString == "DSLR-A100")
                {
                    DecodeA100();
                    return;
                }
                else
                {
                    DecodeCryptedUncompressed();
                    return;
                }
            }
            IFD raw = data[0];
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);
            if (1 == compression)
            {
                DecodeUncompressed(raw);
                return;
            }

            if (32767 != compression)
                throw new RawDecoderException("ARW Decoder: Unsupported compression");

            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("ARW Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("ARW Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:%u " + offsets.dataCount);
            }
            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            int bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);

            rawImage.fullSize.ColorDepth = (ushort)bitPerPixel;
            // Sony E-550 marks compressed 8bpp ARW with 12 bit per pixel
            // this makes the compression detect it as a ARW v1.
            // This camera has however another MAKER entry, so we MAY be able
            // to detect it this way in the future.
            data = ifd.GetIFDsWithTag(TagType.MAKE);
            if (data.Count > 1)
            {
                for (Int32 i = 0; i < data.Count; i++)
                {
                    string make = data[i].GetEntry(TagType.MAKE).DataAsString;
                    // Check for maker "SONY" without spaces 
                    if (make != "SONY")
                        bitPerPixel = 8;
                }
            }

            bool arw1 = counts.GetInt(0) * 8 != width * height * bitPerPixel;
            if (arw1)
                height += 8;

            rawImage.fullSize.dim = new Point2D(width, height);
            rawImage.Init(false);


            UInt16[] curve = new UInt16[0x4001];
            Tag c = raw.GetEntry(TagType.SONY_CURVE);
            uint[] sony_curve = { 0, 0, 0, 0, 0, 4095 };

            for (int i = 0; i < 4; i++)
                sony_curve[i + 1] = (uint)(c.GetShort(i) >> 2) & 0xfff;

            for (int i = 0; i < 0x4001; i++)
                curve[i] = (ushort)i;

            for (int i = 0; i < 5; i++)
            {
                for (uint j = sony_curve[i] + 1; j <= sony_curve[i + 1]; j++)
                {
                    curve[j] = (ushort)(curve[j - 1] + (1 << i));
                }
            }
            var table = new TableLookUp(curve, 0x4000, true);

            long c2 = counts.GetUInt(0);
            uint off = offsets.GetUInt(0);

            if (!reader.IsValid(off))
                throw new RawDecoderException("Sony ARW decoder: Data offset after EOF, file probably truncated");

            if (!reader.IsValid(off, c2))
                c2 = reader.BaseStream.Length - off;

            ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, off);
            if (arw1)
                DecodeARW(input, width, height);
            else
                DecodeARW2(input, width, height, bitPerPixel);

            //table was already applyed
            rawImage.table = null;
        }

        private void DecodeCryptedUncompressed()
        {
            var data = ifd.GetIFDsWithTag(TagType.IMAGEWIDTH);
            if (data.Count == 0)
                throw new RawDecoderException("ARW: SRF format, couldn't find width/height");
            var raw = data[0];

            var size = new Point2D(raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0), raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0));
            uint len = (size.Area * 2);

            // Constants taken from dcraw
            uint offtemp = 862144;
            uint key_off = 200896;
            uint head_off = 164600;

            // Replicate the dcraw contortions to get the "decryption" key
            base.reader.Position = key_off; ;
            int offset = base.reader.ReadByte() * 4;
            base.reader.Position = key_off + offset;
            byte[] d = base.reader.ReadBytes(4);
            uint key = (((uint)(d[0]) << 24) | ((uint)(d[1]) << 16) | ((uint)(d[2]) << 8) | d[3]);
            base.reader.Position = head_off;
            byte[] head = base.reader.ReadBytes(40);

            SonyDecrypt(head, 10, key);

            for (int i = 26; i-- > 22;)
                key = key << 8 | head[i];

            // "Decrypt" the whole image buffer in place
            base.reader.Position = offtemp;
            byte[] imageData = base.reader.ReadBytes((int)len);

            SonyDecrypt(imageData, len / 4, key);

            // And now decode as a normal 16bit raw
            rawImage.fullSize.dim = new Point2D(size);
            rawImage.Init(false);
            using (ImageBinaryReader reader = new ImageBinaryReader(imageData, len))
            {
                RawDecompressor.Decode16BitRawUnpacked(reader, size, new Point2D(), rawImage);
            }
        }

        private void DecodeA100()
        {
            // We've caught the elusive A100 in the wild, a transitional format
            // between the simple sanity of the MRW custom format and the wordly
            // wonderfullness of the Tiff-based ARW format, let's shoot from the hip
            var data = ifd.GetIFDsWithTag(TagType.SUBIFDS);
            if (data.Count == 0)
                throw new RawDecoderException("ARW: A100 format, couldn't find offset");
            var raw = data[0];
            uint offset = raw.GetEntry(TagType.SUBIFDS).GetUInt(0);
            uint w = 3881;
            uint h = 2608;

            rawImage.fullSize.dim = new Point2D(w, h);
            rawImage.Init(false);
            reader = new ImageBinaryReader(reader.BaseStream, offset);

            DecodeARW(reader, w, h);
        }

        void DecodeUncompressed(IFD raw)
        {
            uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            uint off = raw.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);

            rawImage.fullSize.dim = new Point2D(width, height);
            rawImage.Init(false);
            ImageBinaryReader input = new ImageBinaryReader(reader.BaseStream, off);
            /*
                RawDecompressor.Decode14BitRawBEunpacked(input, width, height, rawImage);
           */
            RawDecompressor.Decode16BitRawUnpacked(input, new Point2D(width, height), new Point2D(), rawImage);
        }

        void DecodeARW(ImageBinaryReader input, long w, long h)
        {
            BitPumpMSB bits = new BitPumpMSB(input);

            int sum = 0;
            for (long x = w; (x--) != 0;)
            {
                for (int y = 0; y < h + 1; y += 2)
                {
                    bits.Fill();
                    if (y == h) y = 1;
                    int len = 4 - (int)bits.GetBits(2);
                    if (len == 3 && bits.GetBit() != 0) len = 0;
                    if (len == 4)
                        while (len < 17 && bits.GetBit() == 0) len++;
                    int diff = (int)bits.GetBits(len);
                    if (len != 0 && (diff & (1 << (len - 1))) == 0)
                        diff -= (1 << len) - 1;
                    sum += diff;
                    Debug.Assert((sum >> 12) == 0);
                    if (y < h) rawImage.fullSize.rawView[x + y * rawImage.fullSize.dim.width] = (ushort)sum;
                }
            }
        }

        void DecodeARW2(ImageBinaryReader input, long w, long h, int bpp)
        {
            input.Position = 0;
            if (bpp == 8)
            {
                // Since ARW2 compressed images have predictable offsets, we decode them threaded        
                BitPumpPlain bits = new BitPumpPlain(reader);
                //todo add parralel (parrallel.For not working because onlyone bits so not thread safe;
                //set one bits pump per row (may be slower)
                for (uint y = 0; y < rawImage.fullSize.dim.height; y++)
                {
                    // Realign
                    bits.Offset = (int)(rawImage.fullSize.dim.width * 8 * y) >> 3;
                    uint random = bits.PeekBits(24);

                    // Process 32 pixels (16x2) per loop.
                    for (uint x = 0; x < rawImage.fullSize.dim.width - 30;)
                    {
                        int _max = (int)bits.GetBits(11);
                        int _min = (int)bits.GetBits(11);
                        int _imax = (int)bits.GetBits(4);
                        int _imin = (int)bits.GetBits(4);
                        int sh;
                        for (sh = 0; sh < 4 && 0x80 << sh <= _max - _min; sh++) ;
                        for (int i = 0; i < 16; i++)
                        {
                            int p;
                            if (i == _imax) p = _max;
                            else if (i == _imin) p = _min;
                            else
                            {
                                p = (int)(bits.GetBits(7) << sh) + _min;
                                if (p > 0x7ff)
                                    p = 0x7ff;
                            }
                            rawImage.SetWithLookUp((ushort)(p << 1), rawImage.fullSize.rawView, (int)((y * rawImage.fullSize.dim.width) + x + i * 2), ref random);
                        }
                        x += (x & 1) != 0 ? (uint)31 : 1;  // Skip to next 32 pixels
                    }
                }
            }
            else if (bpp == 12)
            {
                byte[] inputTempArray = input.ReadBytes((int)input.BaseStream.Length);

                if (input.RemainingSize < (w * 3 / 2))
                    throw new RawDecoderException("Sony Decoder: Image data section too small, file probably truncated");

                if (input.RemainingSize < (w * h * 3 / 2))
                    h = input.RemainingSize / (w * 3 / 2) - 1;
                int i = 0;
                for (uint y = 0; y < h; y++)
                {
                    for (uint x = 0; x < w; x += 2)
                    {
                        uint g1 = inputTempArray[i++];
                        uint g2 = inputTempArray[i++];
                        rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                        uint g3 = inputTempArray[i++];
                        rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                    }

                }
            }
            else
                throw new RawDecoderException("Unsupported bit depth");
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();
            if (rawImage.metadata.Model == null)
                throw new RawDecoderException("ARW Meta Decoder: Model name found");
            if (rawImage.metadata.Make == null)
                throw new RawDecoderException("ARW Decoder: Make name not found");

            //get cfa
            var cfa = ifd.GetEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.Red, CFAColor.Green, CFAColor.Green, CFAColor.Blue);
            }
            else
            {
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }


            // Set the whitebalance
            if (rawImage.metadata.Model == "DSLR-A100")
            {
                Tag priv = ifd.GetEntryRecursive(TagType.DNGPRIVATEDATA);
                if (priv != null)
                {
                    byte[] offdata = priv.GetByteArray();
                    uint off = ((uint)(offdata[3] << 24) | (uint)(offdata[2] << 16) |
                            (uint)(offdata[1] << 8) | offdata[0]);
                    uint length = (uint)reader.BaseStream.Length - off;
                    reader.BaseStream.Position = off;
                    byte[] stringdata = reader.ReadBytes((int)length);
                    Int32 currpos = 8;
                    while (currpos + 20 < length)
                    {
                        uint tag = (((uint)(stringdata[currpos]) << 24) | ((uint)(stringdata[currpos + 1]) << 16) | ((uint)(stringdata[currpos + 2]) << 8) |
                            stringdata[currpos + 3]);
                        uint len = ((uint)(stringdata[currpos + 4 + 3] << 24) | (uint)(stringdata[currpos + 4 + 2] << 16) |
                            (uint)(stringdata[currpos + 4 + 1] << 8) | stringdata[currpos + 4]);

                        if (tag == 0x574247)
                        { /* WBG */
                            UInt16[] tmp = new UInt16[4];
                            for (int i = 0; i < 4; i++)
                                tmp[i] = (ushort)(stringdata[(currpos + 12 + i * 2) + 1] << 8 | stringdata[currpos + 12 + i * 2]);
                            rawImage.metadata.WbCoeffs = new WhiteBalance(tmp[0], tmp[1], tmp[3], rawImage.fullSize.ColorDepth);
                            break;
                        }
                        currpos += (int)Math.Max(len + 8, 1); // To make sure we make progress
                    }
                }
            }
            else
            { // Everything else but the A100
                try
                {
                    GetWB();
                }
                catch (Exception e)
                {
                    rawImage.errors.Add(e.Message);
                    // We caught an exception reading WB, just ignore it
                }
            }
            SetMetadata(rawImage.metadata.Model);
        }

        protected void SetMetadata(string model)
        {
            switch (rawImage.fullSize.dim.width)
            {
                case 3984:
                    rawImage.fullSize.dim.width = 3925;
                    //order = 0x4d4d;
                    break;
                case 4288:
                    rawImage.fullSize.dim.width -= 32;
                    break;
                case 4600:
                    if (model.Contains("DSLR-A350"))
                        rawImage.fullSize.dim.height -= 4;
                    rawImage.black = 0;
                    break;
                case 4928:
                    if (rawImage.fullSize.dim.height < 3280) rawImage.fullSize.dim.width -= 8;
                    break;
                case 5504:
                    rawImage.fullSize.dim.width -= (uint)((rawImage.fullSize.dim.height > 3664) ? 8 : 32);
                    if (model.StartsWith("DSC"))
                        rawImage.black = 200 << (rawImage.fullSize.ColorDepth - 12);
                    break;
                case 6048:
                    rawImage.fullSize.dim.width -= 24;
                    if (model.Contains("RX1") || model.Contains("A99"))
                        rawImage.fullSize.dim.width -= 6;
                    break;
                case 7392:
                    rawImage.fullSize.dim.width -= 30;
                    break;
                case 8000:
                    rawImage.fullSize.dim.width -= 32;
                    if (model.StartsWith("DSC"))
                    {
                        rawImage.fullSize.ColorDepth = 14;
                        //load_raw = &CLASS unpacked_load_raw;
                        rawImage.black = 512;
                    }
                    break;
            }
            if (model == "DSLR-A100")
            {
                if (rawImage.fullSize.dim.width == 3880)
                {
                    rawImage.fullSize.dim.height--;
                    rawImage.fullSize.dim.width = ++rawImage.fullSize.UncroppedDim.width;
                }
                else
                {
                    rawImage.fullSize.dim.height -= 4;
                    rawImage.fullSize.dim.width -= 4;
                    //order = 0x4d4d;
                    //load_flags = 2;
                }
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.Green, CFAColor.Red, CFAColor.Blue, CFAColor.Green);
            }
        }

        unsafe static void SonyDecrypt(byte[] ifpData, uint len, uint key)
        {
            fixed (byte* temp = ifpData)
            {
                uint* buffer = (uint*)temp;
                uint* pad = stackalloc uint[128];

                // Initialize the decryption pad from the key
                for (int p = 0; p < 4; p++)
                {
                    pad[p] = key = key * 48828125 + 1;
                }
                pad[3] = pad[3] << 1 | (pad[0] ^ pad[2]) >> 31;

                for (int p = 4; p < 127; p++)
                {
                    pad[p] = (pad[p - 4] ^ pad[p - 2]) << 1 | (pad[p - 3] ^ pad[p - 1]) >> 31;
                }
                for (int p = 0; p < 127; p++)
                {
                    pad[p] = (uint)(((((byte*)&pad[p])[0]) << 24) | ((((byte*)&pad[p])[1]) << 16) |
                        ((((byte*)&pad[p])[2]) << 8) | ((byte*)&pad[p])[3]);
                }
                int p2 = 127;
                // Decrypt the buffer in place using the pad
                while ((len--) != 0)
                {
                    pad[p2 & 127] = pad[(p2 + 1) & 127] ^ pad[(p2 + 1 + 64) & 127];
                    *buffer++ ^= pad[p2 & 127];
                    p2++;
                }
            }
        }

        void GetWB()
        {
            // Set the whitebalance for all the modern ARW formats (everything after A100)
            Tag priv = ifd.GetEntryRecursive(TagType.DNGPRIVATEDATA);
            if (priv != null)
            {
                byte[] data = priv.GetByteArray();
                uint off = ((((uint)(data)[3]) << 24) | (((uint)(data)[2]) << 16) | (((uint)(data)[1]) << 8) | (data)[0]);
                IFD sony_private;
                sony_private = new IFD(reader, off, ifd.endian, ifd.Depth);

                Tag sony_offset = sony_private.GetEntryRecursive(TagType.SONY_OFFSET);
                Tag sony_length = sony_private.GetEntryRecursive(TagType.SONY_LENGTH);
                Tag sony_key = sony_private.GetEntryRecursive(TagType.SONY_KEY);
                if (sony_offset == null || sony_length == null || sony_key == null || sony_key.dataCount != 4)
                    throw new RawDecoderException("Couldn't find the correct metadata for white balance decoding");

                off = sony_offset.GetUInt(0);
                uint len = sony_length.GetUInt(0);
                data = sony_key.GetByteArray();
                uint key = (uint)((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | (data)[0]);
                reader.BaseStream.Position = off;
                byte[] ifp_data = reader.ReadBytes((int)len);
                SonyDecrypt(ifp_data, len / 4, key);
                using (var reader = new ImageBinaryReader(ifp_data))
                {
                    sony_private = new IFD(reader, 0, ifd.endian, 0, -(int)off);
                }
                Tag wb = sony_private.GetEntry(TagType.SONYGRBGLEVELS);
                if (wb != null)
                {

                    if (wb.dataCount != 4)
                        throw new RawDecoderException("White balance has " + wb.dataCount + " entries instead of 4");
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(1), wb.GetInt(0), wb.GetInt(2), rawImage.fullSize.ColorDepth);
                }
                else if ((wb = sony_private.GetEntry(TagType.SONYRGGBLEVELS)) != null)
                {
                    if (wb.dataCount != 4)
                        throw new RawDecoderException("White balance has " + wb.dataCount + " entries instead of 4");
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(0), wb.GetInt(1), wb.GetInt(3), rawImage.fullSize.ColorDepth);
                }

                //TODO read the color matrix 0x7800

                Tag black = sony_private.GetEntry((TagType)0x7300) ?? sony_private.GetEntry((TagType)0x7310);
                if (black != null) rawImage.black = black.GetLong(0);

                Tag white = sony_private.GetEntry((TagType)0x787f);
                if (white != null) rawImage.whitePoint = white.GetLong(0);
            }
        }

        private CamRGB[] colorM = {
            /*
    { "Sony DSC-F828", 0, 0,
    { 7924,-1910,-777,-8226,15459,2998,-1517,2199,6818,-7242,11401,3481 } },
    { "Sony DSC-R1", 0, 0,
    { 8512,-2641,-694,-8042,15670,2526,-1821,2117,7414 } },
    { "Sony DSC-V3", 0, 0,
    { 7511,-2571,-692,-7894,15088,3060,-948,1111,8128 } },
    { "Sony DSC-RX100M", 0, 0,		// M2, M3, and M4 
	{ 6596,-2079,-562,-4782,13016,1933,-970,1581,5181 } },
    { "Sony DSC-RX100", 0, 0,
    { 8651,-2754,-1057,-3464,12207,1373,-568,1398,4434 } },
    { "Sony DSC-RX10", 0, 0,		// also RX10M2 
	{ 6679,-1825,-745,-5047,13256,1953,-1580,2422,5183 } },
    { "Sony DSC-RX1RM2", 0, 0,
    { 6629,-1900,-483,-4618,12349,2550,-622,1381,6514 } },
    { "Sony DSC-RX1", 0, 0,
    { 6344,-1612,-462,-4863,12477,2681,-865,1786,6899 } },*/
        new CamRGB( "Sony DSLR-A100", 0, 0xfeb,  new double[]  { 9437,-2811,-774,-8405,16215,2290,-710,596,7181 } ),/*
    { "Sony DSLR-A290", 0, 0,    { 6038,-1484,-579,-9145,16746,2512,-875,746,7218 } },
    { "Sony DSLR-A2", 0, 0,    { 9847,-3091,-928,-8485,16345,2225,-715,595,7103 } },
    { "Sony DSLR-A300", 0, 0,    { 9847,-3091,-928,-8485,16345,2225,-715,595,7103 } },
    { "Sony DSLR-A330", 0, 0,    { 9847,-3091,-929,-8485,16346,2225,-714,595,7103 } },*/
    new CamRGB( "Sony DSLR-A350", 0, 0xffc,    new double[]{ 6038,-1484,-578,-9146,16746,2513,-875,746,7217 } ),/*
    { "Sony DSLR-A380", 0, 0,    { 6038,-1484,-579,-9145,16746,2512,-875,746,7218 } },
    { "Sony DSLR-A390", 0, 0,    { 6038,-1484,-579,-9145,16746,2512,-875,746,7218 } },*/
    new CamRGB( "Sony DSLR-A450", 0, 0xfeb,    new double[]{ 4950,-580,-103,-5228,12542,3029,-709,1435,7371 } ),
    new CamRGB( "Sony DSLR-A580", 0, 0xfeb,    new double[]{ 5932,-1492,-411,-4813,12285,2856,-741,1524,6739 } ),
    new CamRGB( "Sony DSLR-A500", 0, 0xfeb,    new double[]{ 6046,-1127,-278,-5574,13076,2786,-691,1419,7625 } ),
    new CamRGB( "Sony DSLR-A5", 0, 0xfeb,    new double[]{ 4950,-580,-103,-5228,12542,3029,-709,1435,7371 } ),/*
    { "Sony DSLR-A700", 0, 0,    { 5775,-805,-359,-8574,16295,2391,-1943,2341,7249 } },
    { "Sony DSLR-A850", 0, 0,    { 5413,-1162,-365,-5665,13098,2866,-608,1179,8440 } },
    { "Sony DSLR-A900", 0, 0,    { 5209,-1072,-397,-8845,16120,2919,-1618,1803,8654 } },
    { "Sony ILCA-68", 0, 0,    { 6435,-1903,-536,-4722,12449,2550,-663,1363,6517 } },
    { "Sony ILCA-77M2", 0, 0,    { 5991,-1732,-443,-4100,11989,2381,-704,1467,5992 } },
    { "Sony ILCE-6300", 0, 0,    { 5973,-1695,-419,-3826,11797,2293,-639,1398,5789 } },
    { "Sony ILCE-7M2", 0, 0,    { 5271,-712,-347,-6153,13653,2763,-1601,2366,7242 } },
    { "Sony ILCE-7S", 0, 0,	// also ILCE-7SM2 
	{ 5838,-1430,-246,-3497,11477,2297,-748,1885,5778 } },
    { "Sony ILCE-7RM2", 0, 0,
    { 6629,-1900,-483,-4618,12349,2550,-622,1381,6514 } },
    { "Sony ILCE-7R", 0, 0,
    { 4913,-541,-202,-6130,13513,2906,-1564,2151,7183 } },
    { "Sony ILCE-7", 0, 0,
    { 5271,-712,-347,-6153,13653,2763,-1601,2366,7242 } },
    { "Sony ILCE", 0, 0,	// 3000, 5000, 5100, 6000, and QX1 
	{ 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony NEX-5N", 0, 0,
    { 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony NEX-5R", 0, 0,
    { 6129,-1545,-418,-4930,12490,2743,-977,1693,6615 } },
    { "Sony NEX-5T", 0, 0,
    { 6129,-1545,-418,-4930,12490,2743,-977,1693,6615 } },
    { "Sony NEX-3N", 0, 0,
    { 6129,-1545,-418,-4930,12490,2743,-977,1693,6615 } },*/
    new CamRGB( "Sony NEX-3", 138, 0,   new double[]        { 6907,-1256,-645,-4940,12621,2320,-1710,2581,6230 } ),
    new CamRGB( "Sony NEX-5", 116, 0,   new double[]        { 6807,-1350,-342,-4216,11649,2567,-1089,2001,6420 } ),/*
    { "Sony NEX-3", 0, 0,		// Adobe 
	{ 6549,-1550,-436,-4880,12435,2753,-854,1868,6976 } },
    { "Sony NEX-5", 0, 0,		// Adobe 
	{ 6549,-1550,-436,-4880,12435,2753,-854,1868,6976 } },
    { "Sony NEX-6", 0, 0,    { 6129,-1545,-418,-4930,12490,2743,-977,1693,6615 } },
    { "Sony NEX-7", 0, 0,    { 5491,-1192,-363,-4951,12342,2948,-911,1722,7192 } },
    { "Sony NEX", 0, 0,	// NEX-C3, NEX-F3 
	{ 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony SLT-A33", 0, 0,    { 6069,-1221,-366,-5221,12779,2734,-1024,2066,6834 } },
    { "Sony SLT-A35", 0, 0,    { 5986,-1618,-415,-4557,11820,3120,-681,1404,6971 } },
    { "Sony SLT-A37", 0, 0,    { 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony SLT-A55", 0, 0,    { 5932,-1492,-411,-4813,12285,2856,-741,1524,6739 } },
    { "Sony SLT-A57", 0, 0,    { 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony SLT-A58", 0, 0,    { 5991,-1456,-455,-4764,12135,2980,-707,1425,6701 } },
    { "Sony SLT-A65", 0, 0,    { 5491,-1192,-363,-4951,12342,2948,-911,1722,7192 } },
    { "Sony SLT-A77", 0, 0,    { 5491,-1192,-363,-4951,12342,2948,-911,1722,7192 } },
    { "Sony SLT-A99", 0, 0,    { 6344,-1612,-462,-4863,12477,2681,-865,1786,6899 } }*/
        };
    }
}

