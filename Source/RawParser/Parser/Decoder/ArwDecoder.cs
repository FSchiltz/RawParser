using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet
{
    class ArwDecoder : TiffDecoder
    {
        int shiftDownScale;

        internal ArwDecoder(Stream file) : base(file)
        {
            shiftDownScale = 0;
        }

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
            IFD raw = null;
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count == 0)
            {
                Tag model = ifd.GetEntryRecursive(TagType.MODEL);

                if (model != null && model.DataAsString == "DSLR-A100")
                {
                    // We've caught the elusive A100 in the wild, a transitional format
                    // between the simple sanity of the MRW custom format and the wordly
                    // wonderfullness of the Tiff-based ARW format, let's shoot from the hip
                    data = ifd.GetIFDsWithTag(TagType.SUBIFDS);
                    if (data.Count == 0)
                        throw new RawDecoderException("ARW: A100 format, couldn't find offset");
                    raw = data[0];
                    uint offset = raw.GetEntry(TagType.SUBIFDS).GetUInt(0);
                    int w = 3881;
                    int h = 2608;

                    rawImage.raw.dim = new Point2D(w, h);
                    rawImage.Init();
                    reader = new TIFFBinaryReader(reader.BaseStream, offset);

                    try
                    {
                        DecodeARW(reader, w, h);
                    }
                    catch (IOException e)
                    {
                        rawImage.errors.Add(e.Message);
                        // Let's ignore it, it may have delivered somewhat useful data.
                    }

                    return;
                }
                else if (hints.ContainsKey("srf_format"))
                {

                    data = ifd.GetIFDsWithTag(TagType.IMAGEWIDTH);
                    if (data.Count == 0)
                        throw new RawDecoderException("ARW: SRF format, couldn't find width/height");
                    raw = data[0];

                    int w = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
                    int h = raw.GetEntry(TagType.IMAGELENGTH).GetInt(0);
                    uint len = (uint)(w * h * 2);

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
                    rawImage.raw.dim = new Point2D(w, h);
                    rawImage.Init();
                    TIFFBinaryReader reader = new TIFFBinaryReader(imageData, len);
                    Decode16BitRawBEunpacked(reader, w, h);
                    reader.Dispose();
                    return;
                }
                else
                {
                    throw new RawDecoderException("ARW Decoder: No image data found");
                }
            }

            raw = data[0];
            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);
            if (1 == compression)
            {
                try
                {
                    DecodeUncompressed(raw);
                }
                catch (IOException e)
                {
                    rawImage.errors.Add(e.Message);
                }

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
            int width = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            int height = raw.GetEntry(TagType.IMAGELENGTH).GetInt(0);
            int bitPerPixel = raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);
            rawImage.ColorDepth = (ushort)bitPerPixel;
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
                    /* Check for maker "SONY" without spaces */
                    if (make != "SONY")
                        bitPerPixel = 8;
                }
            }

            bool arw1 = counts.GetInt(0) * 8 != width * height * bitPerPixel;
            if (arw1)
                height += 8;

            rawImage.raw.dim = new Point2D((int)width, (int)height);
            rawImage.Init();

            UInt16[] curve = new UInt16[0x4001];
            Tag c = raw.GetEntry(TagType.SONY_CURVE);
            uint[] sony_curve = { 0, 0, 0, 0, 0, 4095 };

            for (int i = 0; i < 4; i++)
                sony_curve[i + 1] = (uint)(c.GetShort(i) >> 2) & 0xfff;

            for (int i = 0; i < 0x4001; i++)
                curve[i] = (ushort)i;

            for (int i = 0; i < 5; i++)
                for (uint j = sony_curve[i] + 1; j <= sony_curve[i + 1]; j++)
                    curve[j] = (ushort)(curve[j - 1] + (1 << i));


            rawImage.SetTable(curve, 0x4000, true);

            uint c2 = counts.GetUInt(0);
            uint off = offsets.GetUInt(0);

            if (!reader.IsValid(off))
                throw new RawDecoderException("Sony ARW decoder: Data offset after EOF, file probably truncated");

            if (!reader.IsValid(off, c2))
                c2 = (uint)(reader.BaseStream.Length - off);

            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, off);

            try
            {
                if (arw1)
                    DecodeARW( input, width, height);
                else
                    DecodeARW2( input, width, height, bitPerPixel);
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }

            // Set the table, if it should be needed later.
            Debug.WriteLine("Set table is not correct");
            // mRaw.setTable(null);
        }

        void DecodeUncompressed(IFD raw)
        {
            int width = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            int height = raw.GetEntry(TagType.IMAGELENGTH).GetInt(0);
            uint off = raw.GetEntry(TagType.STRIPOFFSETS).GetUInt(0);

            rawImage.raw.dim = new Point2D(width, height);
            rawImage.Init();
            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, off);

            if (hints.ContainsKey("sr2_format"))
                Decode14BitRawBEunpacked(input, width, height);
            else
                Decode16BitRawUnpacked(input, width, height);
        }

        unsafe void DecodeARW(TIFFBinaryReader input, int w, int h)
        {
            BitPumpMSB bits = new BitPumpMSB(input);
            fixed (UInt16* dest = rawImage.raw.data)
            {
                //uint pitch = rawImage.pitch / sizeof(UInt16);
                int sum = 0;
                for (int x = w; (x--) != 0;)
                {
                    for (int y = 0; y < h + 1; y += 2)
                    {
                        bits.CheckPos();
                        bits.FillCheck();
                        if (y == h) y = 1;
                        uint len = 4 - bits.GetBitsNoFill(2);
                        if (len == 3 && bits.GetBitNoFill() != 0) len = 0;
                        if (len == 4)
                            while (len < 17 && bits.GetBitNoFill() == 0) len++;
                        uint diff = bits.GetBits(len);
                        if (len != 0 && (diff & (1 << (int)(len - 1))) == 0)
                            diff -= (uint)(1 << (int)len) - 1;
                        sum += (int)diff;
                        // Debug.Assert((sum >> 12) == 0);
                        if (y < h) dest[x + y * rawImage.raw.dim.width] = (ushort)sum;
                    }
                }
            }
        }

        void DecodeARW2(TIFFBinaryReader input, int w, int h, int bpp)
        {
            input.Position = 0;
            if (bpp == 8)
            {
                /* Since ARW2 compressed images have predictable offsets, we decode them threaded */
                // throw new RawDecoderException("8 bits image are not yet supported");       
                BitPumpPlain bits = new BitPumpPlain(reader);
                //todo add parralel (parrallel.For not working because onlyone bits so not thread safe;
                //set one bits pump per row (may be slower)
                for (uint y = 0; y < rawImage.raw.dim.height; y++)
                {
                    // Realign
                    bits.setAbsoluteOffset((uint)(rawImage.raw.dim.width * 8 * y) >> 3);
                    uint random = bits.peekBits(24);

                    // Process 32 pixels (16x2) per loop.
                    for (uint x = 0; x < rawImage.raw.dim.width - 30;)
                    {
                        bits.checkPos();
                        int _max = (int)bits.getBits(11);
                        int _min = (int)bits.getBits(11);
                        int _imax = (int)bits.getBits(4);
                        int _imin = (int)bits.getBits(4);
                        int sh;
                        for (sh = 0; sh < 4 && 0x80 << sh <= _max - _min; sh++) ;
                        for (int i = 0; i < 16; i++)
                        {
                            int p;
                            if (i == _imax) p = _max;
                            else if (i == _imin) p = _min;
                            else
                            {
                                p = (int)(bits.getBits(7) << sh) + _min;
                                if (p > 0x7ff)
                                    p = 0x7ff;
                            }
                            rawImage.SetWithLookUp((ushort)(p << 1), rawImage.raw.data, (int)((y * rawImage.raw.dim.width) + x + i * 2), ref random);

                        }
                        x += (x & 1) != 0 ? (uint)31 : 1;  // Skip to next 32 pixels
                    }
                }
            }
            else if (bpp == 12)
            {
                unsafe
                {
                    byte[] inputTempArray = input.ReadBytes((int)input.BaseStream.Length);
                    fixed (byte* inputTemp = inputTempArray)
                    {
                        byte* t2 = inputTemp;
                        if (input.GetRemainSize() < (w * 3 / 2))
                            throw new RawDecoderException("Sony Decoder: Image data section too small, file probably truncated");

                        if (input.GetRemainSize() < (w * h * 3 / 2))
                            h = input.GetRemainSize() / (w * 3 / 2) - 1;

                        for (uint y = 0; y < h; y++)
                        {
                            for (uint x = 0; x < w; x += 2)
                            {
                                uint g1 = *(t2++);
                                uint g2 = *(t2++);
                                rawImage.raw.data[y * rawImage.raw.dim.width + x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                                uint g3 = *(t2++);
                                rawImage.raw.data[y * rawImage.raw.dim.width + x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                            }

                        }
                    }
                }
                // Shift scales, since black and white are the same as compressed precision
                //shiftDownScale = 2;
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
                Debug.WriteLine("CFA pattern is not found");
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.cfa.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }

            /* rawImage.whitePoint >>= shiftDownScale;
             rawImage.blackLevel >>= shiftDownScale;*/

            // Set the whitebalance
            if (rawImage.metadata.Model == "DSLR-A100")
            { // Handle the MRW style WB of the A100

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

                            rawImage.metadata.WbCoeffs[0] = (float)tmp[0] / tmp[1];
                            rawImage.metadata.WbCoeffs[1] = (float)tmp[1] / tmp[1];
                            rawImage.metadata.WbCoeffs[2] = (float)tmp[3] / tmp[1];
                            break;
                        }
                        currpos += (int)Math.Max(len + 8, 1); // Math.Max(,1) to make sure we make progress
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
            if (rawImage.raw.dim.width > 3888)
            {
                rawImage.blackLevel = 128 << (rawImage.ColorDepth - 12);
            }

            switch (rawImage.raw.dim.width)
            {
                case 3984:
                    rawImage.raw.dim.width = 3925;
                    //order = 0x4d4d;
                    break;
                case 4288:
                    rawImage.raw.dim.width -= 32;
                    break;
                case 4600:
                    if (model.Contains("DSLR-A350"))
                        rawImage.raw.dim.height -= 4;
                    rawImage.blackLevel = 0;
                    break;
                case 4928:
                    if (rawImage.raw.dim.height < 3280) rawImage.raw.dim.width -= 8;
                    break;
                case 5504:
                    rawImage.raw.dim.width -= (rawImage.raw.dim.height > 3664) ? 8 : 32;
                    if (model.StartsWith("DSC"))
                        rawImage.blackLevel = 200 << (rawImage.ColorDepth - 12);
                    break;
                case 6048:
                    rawImage.raw.dim.width -= 24;
                    if (model.Contains("RX1") || model.Contains("A99"))
                        rawImage.raw.dim.width -= 6;
                    break;
                case 7392:
                    rawImage.raw.dim.width -= 30;
                    break;
                case 8000:
                    rawImage.raw.dim.width -= 32;
                    if (model.StartsWith("DSC"))
                    {
                        rawImage.ColorDepth = 14;
                        //load_raw = &CLASS unpacked_load_raw;
                        rawImage.blackLevel = 512;
                    }
                    break;
            }
            if (model == "DSLR-A100")
            {
                if (rawImage.raw.dim.width == 3880)
                {
                    rawImage.raw.dim.height--;
                    rawImage.raw.dim.width = ++rawImage.raw.uncroppedDim.width;
                }
                else
                {
                    rawImage.raw.dim.height -= 4;
                    rawImage.raw.dim.width -= 4;
                    //order = 0x4d4d;
                    //load_flags = 2;
                }
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.GREEN, CFAColor.RED, CFAColor.BLUE, CFAColor.GREEN);
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
                    pad[p] = ((((uint)((byte*)&pad[p])[0]) << 24) | (((uint)((byte*)&pad[p])[1]) << 16) |
                        (((uint)((byte*)&pad[p])[2]) << 8) | ((byte*)&pad[p])[3]);
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

        unsafe void GetWB()
        {
            // Set the whitebalance for all the modern ARW formats (everything after A100)
            Tag priv = ifd.GetEntryRecursive(TagType.DNGPRIVATEDATA);
            if (priv != null)
            {
                byte[] data = priv.GetByteArray();
                uint off = ((((uint)(data)[3]) << 24) | (((uint)(data)[2]) << 16) | (((uint)(data)[1]) << 8) | (data)[0]);
                IFD sony_private;
                sony_private = new IFD(reader, off, ifd.endian,ifd.Depth);

                Tag sony_offset = sony_private.GetEntryRecursive(TagType.SONY_OFFSET);
                Tag sony_length = sony_private.GetEntryRecursive(TagType.SONY_LENGTH);
                Tag sony_key = sony_private.GetEntryRecursive(TagType.SONY_KEY);
                if (sony_offset == null || sony_length == null || sony_key == null || sony_key.dataCount != 4)
                    throw new RawDecoderException("ARW: couldn't find the correct metadata for WB decoding");

                off = sony_offset.GetUInt(0);
                uint len = sony_length.GetUInt(0);
                data = sony_key.GetByteArray();
                uint key = ((((uint)(data)[3]) << 24) | (((uint)(data)[2]) << 16) | (((uint)(data)[1]) << 8) | (data)[0]);
                reader.BaseStream.Position = off;
                byte[] ifp_data = reader.ReadBytes((int)len);
                SonyDecrypt(ifp_data, len / 4, key);
                using (var reader = new TIFFBinaryReader(ifp_data))
                {
                    sony_private = new IFD(reader, 0, ifd.endian, 0, -(int)off);
                }
                if (sony_private.tags.ContainsKey(TagType.SONYGRBGLEVELS))
                {
                    Tag wb = sony_private.GetEntry(TagType.SONYGRBGLEVELS);
                    if (wb.dataCount != 4)
                        throw new RawDecoderException("ARW: WB has " + wb.dataCount + " entries instead of 4");
                    rawImage.metadata.WbCoeffs[0] = wb.GetFloat(1) / wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[1] = wb.GetFloat(0) / wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[2] = wb.GetFloat(2) / wb.GetFloat(0);
                }
                else if (sony_private.tags.ContainsKey(TagType.SONYRGGBLEVELS))
                {
                    Tag wb = sony_private.GetEntry(TagType.SONYRGGBLEVELS);
                    if (wb.dataCount != 4)
                        throw new RawDecoderException("ARW: WB has " + wb.dataCount + " entries instead of 4");
                    rawImage.metadata.WbCoeffs[0] = wb.GetFloat(0) / wb.GetFloat(1);
                    rawImage.metadata.WbCoeffs[1] = wb.GetFloat(1) / wb.GetFloat(1);
                    rawImage.metadata.WbCoeffs[2] = wb.GetFloat(3) / wb.GetFloat(1);
                }
            }
        }
    }
}

