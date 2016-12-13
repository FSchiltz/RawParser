using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RawNet
{
    class ArwDecoder : RawDecoder
    {
        IFD rootIFD;
        int shiftDownScale;

        internal ArwDecoder(IFD rootIFD, TIFFBinaryReader file, CameraMetaData meta) : base(ref file, meta)
        {
            this.rootIFD = (rootIFD);
            shiftDownScale = 0;
            decoderVersion = 1;
        }

        /**
       * Taken from nikon decoder
       */
        protected override Thumbnail decodeThumbInternal()
        {
            //find the preview ifd Preview is in the rootIFD (smaller preview in subiFD use those)
            List<IFD> possible = rootIFD.getIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT);
            //no thumbnail
            if (possible == null || possible.Count == 0) return null;
            IFD preview = possible[possible.Count - 1];

            var thumb = preview.getEntry(TagType.JPEGINTERCHANGEFORMAT);
            var size = preview.getEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
            if (size == null || thumb == null) return null;

            file.Position = (uint)(thumb.data[0]);
            Thumbnail temp = new Thumbnail()
            {
                data = file.ReadBytes(Convert.ToInt32(size.data[0])),
                type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }

        protected override RawImage decodeRawInternal()
        {
            IFD raw = null;
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.STRIPOFFSETS);

            if (data.Count == 0)
            {
                Tag model = rootIFD.getEntryRecursive(TagType.MODEL);

                if (model != null && model.DataAsString == "DSLR-A100")
                {
                    // We've caught the elusive A100 in the wild, a transitional format
                    // between the simple sanity of the MRW custom format and the wordly
                    // wonderfullness of the Tiff-based ARW format, let's shoot from the hip
                    data = rootIFD.getIFDsWithTag(TagType.SUBIFDS);
                    if (data.Count == 0)
                        throw new RawDecoderException("ARW: A100 format, couldn't find offset");
                    raw = data[0];
                    UInt32 offset = raw.getEntry(TagType.SUBIFDS).getUInt();
                    UInt32 w = 3881;
                    UInt32 h = 2608;

                    mRaw.dim = new Point2D((int)w, (int)h);
                    mRaw.Init();
                    TIFFBinaryReader reader = new TIFFBinaryReader(file.BaseStream, offset, (uint)file.BaseStream.Length);

                    try
                    {
                        DecodeARW(ref reader, w, h);
                    }
                    catch (IOException e)
                    {
                        mRaw.errors.Add(e.Message);
                        // Let's ignore it, it may have delivered somewhat useful data.
                    }

                    return mRaw;
                }
                else if (hints.ContainsKey("srf_format"))
                {

                    data = rootIFD.getIFDsWithTag(TagType.IMAGEWIDTH);
                    if (data.Count == 0)
                        throw new RawDecoderException("ARW: SRF format, couldn't find width/height");
                    raw = data[0];

                    UInt32 w = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
                    UInt32 h = raw.getEntry(TagType.IMAGELENGTH).getUInt();
                    UInt32 len = w * h * 2;

                    // Constants taken from dcraw
                    UInt32 offtemp = 862144;
                    UInt32 key_off = 200896;
                    UInt32 head_off = 164600;

                    // Replicate the dcraw contortions to get the "decryption" key
                    file.Position = key_off; ;
                    UInt32 offset = (uint)file.ReadByte() * 4;
                    file.Position = key_off + offset;
                    byte[] d = file.ReadBytes(4);
                    UInt32 key = (((uint)(d[0]) << 24) | ((uint)(d[1]) << 16) | ((uint)(d[2]) << 8) | (uint)(d[3]));
                    file.Position = head_off;
                    byte[] head = file.ReadBytes(40);

                    SonyDecrypt(head, 10, key);

                    for (int i = 26; i-- > 22;)
                        key = key << 8 | head[i];

                    // "Decrypt" the whole image buffer in place
                    file.Position = offtemp;
                    byte[] image_data = file.ReadBytes((int)len);

                    SonyDecrypt(image_data, len / 4, key);

                    // And now decode as a normal 16bit raw
                    mRaw.dim = new Point2D((int)w, (int)h);
                    mRaw.Init();
                    TIFFBinaryReader reader = new TIFFBinaryReader(TIFFBinaryReader.streamFromArray(image_data), len, (uint)image_data.Length);
                    Decode16BitRawBEunpacked(reader, w, h);

                    return mRaw;
                }
                else
                {
                    throw new RawDecoderException("ARW Decoder: No image data found");
                }
            }

            raw = data[0];
            int compression = raw.getEntry(TagType.COMPRESSION).getInt();
            if (1 == compression)
            {
                try
                {
                    DecodeUncompressed(raw);
                }
                catch (IOException e)
                {
                    mRaw.errors.Add(e.Message);
                }

                return mRaw;
            }

            if (32767 != compression)
                throw new RawDecoderException("ARW Decoder: Unsupported compression");

            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("ARW Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("ARW Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:%u " + offsets.dataCount);
            }
            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();
            UInt32 bitPerPixel = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();
            mRaw.ColorDepth = (ushort)bitPerPixel;
            // Sony E-550 marks compressed 8bpp ARW with 12 bit per pixel
            // this makes the compression detect it as a ARW v1.
            // This camera has however another MAKER entry, so we MAY be able
            // to detect it this way in the future.
            data = rootIFD.getIFDsWithTag(TagType.MAKE);
            if (data.Count > 1)
            {
                for (Int32 i = 0; i < data.Count; i++)
                {
                    string make = data[i].getEntry(TagType.MAKE).DataAsString;
                    /* Check for maker "SONY" without spaces */
                    if (make != "SONY")
                        bitPerPixel = 8;
                }
            }

            bool arw1 = counts.getInt() * 8 != width * height * bitPerPixel;
            if (arw1)
                height += 8;

            mRaw.dim = new Point2D((int)width, (int)height);
            mRaw.Init();

            UInt16[] curve = new UInt16[0x4001];
            Tag c = raw.getEntry(TagType.SONY_CURVE);
            UInt32[] sony_curve = { 0, 0, 0, 0, 0, 4095 };

            for (Int32 i = 0; i < 4; i++)
                sony_curve[i + 1] = (uint)(c.getShort(i) >> 2) & 0xfff;

            for (Int32 i = 0; i < 0x4001; i++)
                curve[i] = (ushort)i;

            for (UInt32 i = 0; i < 5; i++)
                for (UInt32 j = sony_curve[i] + 1; j <= sony_curve[i + 1]; j++)
                    curve[j] = (ushort)(curve[j - 1] + (1 << (int)i));


            mRaw.setTable(curve, 0x4000, true);

            UInt32 c2 = counts.getUInt();
            UInt32 off = offsets.getUInt();

            if (!file.isValid(off))
                throw new RawDecoderException("Sony ARW decoder: Data offset after EOF, file probably truncated");

            if (!file.isValid(off, c2))
                c2 = (uint)(file.BaseStream.Length - off);

            TIFFBinaryReader input = new TIFFBinaryReader(file.BaseStream, off, c2);

            try
            {
                if (arw1)
                    DecodeARW(ref input, width, height);
                else
                    DecodeARW2(ref input, width, height, bitPerPixel);
            }
            catch (IOException e)
            {
                mRaw.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }

            // Set the table, if it should be needed later.
            Debug.WriteLine("Set table is not correct");
            // mRaw.setTable(null);


            return mRaw;
        }

        void DecodeUncompressed(IFD raw)
        {
            UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
            UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();
            UInt32 off = raw.getEntry(TagType.STRIPOFFSETS).getUInt();
            UInt32 c2 = raw.getEntry(TagType.STRIPBYTECOUNTS).getUInt();

            mRaw.dim = new Point2D((int)width, (int)height);
            mRaw.Init();
            TIFFBinaryReader input = new TIFFBinaryReader(file.BaseStream, off, c2);

            if (hints.ContainsKey("sr2_format"))
                Decode14BitRawBEunpacked(input, width, height);
            else
                Decode16BitRawUnpacked(input, width, height);
        }

        unsafe void DecodeARW(ref TIFFBinaryReader input, UInt32 w, UInt32 h)
        {
            BitPumpMSB bits = new BitPumpMSB(ref input);
            fixed (UInt16* dest = mRaw.rawData)
            {
                UInt32 pitch = mRaw.pitch / sizeof(UInt16);
                int sum = 0;
                for (UInt32 x = w; (x--) != 0;)
                {
                    for (UInt32 y = 0; y < h + 1; y += 2)
                    {
                        bits.checkPos();
                        bits.fill();
                        if (y == h) y = 1;
                        UInt32 len = 4 - bits.getBitsNoFill(2);
                        if (len == 3 && bits.getBitNoFill() != 0) len = 0;
                        if (len == 4)
                            while (len < 17 && bits.getBitNoFill() == 0) len++;
                        uint diff = bits.getBits(len);
                        if (len != 0 && (diff & (1 << (int)(len - 1))) == 0)
                            diff -= (uint)(1 << (int)len) - 1;
                        sum += (int)diff;
                        Debug.Assert((sum >> 12) == 0);
                        if (y < h) dest[x + y * pitch] = (ushort)sum;
                    }
                }
            }
        }

        void DecodeARW2(ref TIFFBinaryReader input, UInt32 w, UInt32 h, UInt32 bpp)
        {
            input.Position = 0;
            if (bpp == 8)
            {
                /* Since ARW2 compressed images have predictable offsets, we decode them threaded */
                // throw new RawDecoderException("8 bits image are not yet supported");       
                BitPumpPlain bits = new BitPumpPlain(ref file);
                //todo add parralel (parrallel.For not working because onlyone bits so not thread safe;
                //set one bits pump per row (may be slower)
                for (UInt32 y = 0; y < mRaw.dim.y; y++)
                {
                    // Realign
                    bits.setAbsoluteOffset((uint)(mRaw.dim.x * 8 * y) >> 3);
                    UInt32 random = bits.peekBits(24);

                    // Process 32 pixels (16x2) per loop.
                    for (Int32 x = 0; x < mRaw.dim.x - 30;)
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
                            mRaw.setWithLookUp((ushort)(p << 1), ref mRaw.rawData, (uint)((y * mRaw.dim.x) + x + i * 2), ref random);

                        }
                        x += (x & 1) != 0 ? 31 : 1;  // Skip to next 32 pixels
                    }
                }

            }
            else if (bpp == 12)
            {
                unsafe
                {
                    fixed (ushort* dataShort = mRaw.rawData)
                    {
                        byte* data = ((byte*)dataShort);
                        UInt32 pitch = mRaw.pitch;
                        byte[] inputTempArray = input.ReadBytes((int)input.BaseStream.Length);
                        fixed (byte* inputTemp = inputTempArray)
                        {
                            byte* t2 = inputTemp;
                            if (input.getRemainSize() < (w * 3 / 2))
                                throw new RawDecoderException("Sony Decoder: Image data section too small, file probably truncated");

                            if (input.getRemainSize() < (w * h * 3 / 2))
                                h = (uint)input.getRemainSize() / (w * 3 / 2) - 1;

                            for (UInt32 y = 0; y < h; y++)
                            {
                                byte* temp = &data[y * pitch];

                                UInt16* dest = (UInt16*)temp;
                                for (UInt32 x = 0; x < w; x += 2)
                                {
                                    UInt32 g1 = *(t2++);
                                    UInt32 g2 = *(t2++);
                                    dest[x] = (ushort)(g1 | ((g2 & 0xf) << 8));
                                    UInt32 g3 = *(t2++);
                                    dest[x + 1] = (ushort)((g2 >> 4) | (g3 << 4));
                                }

                            }
                        }
                    }
                }
                // Shift scales, since black and white are the same as compressed precision
                shiftDownScale = 2;
            }
            else
                throw new RawDecoderException("Unsupported bit depth");
        }

        protected override void checkSupportInternal()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("ARW Support check: Model name found");
            string make = data[0].getEntry(TagType.MAKE).DataAsString;
            string model = data[0].getEntry(TagType.MODEL).DataAsString;
            this.checkCameraSupported(metaData, make, model, "");
        }

        protected override void decodeMetaDataInternal()
        {
            //Default
            int iso = 0;

            mRaw.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("ARW Meta Decoder: Model name found");
            if (!data[0].hasEntry(TagType.MAKE))
                throw new RawDecoderException("ARW Decoder: Make name not found");

            string make = data[0].getEntry(TagType.MAKE).DataAsString;
            string model = data[0].getEntry(TagType.MODEL).DataAsString;

            Tag isoTag = rootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                iso = isoTag.getInt();

            setMetaData(metaData, make, model, "", iso);
            mRaw.whitePoint >>= shiftDownScale;
            mRaw.blackLevel >>= shiftDownScale;

            // Set the whitebalance
            if (model == "DSLR-A100")
            { // Handle the MRW style WB of the A100

                Tag priv = rootIFD.getEntryRecursive(TagType.DNGPRIVATEDATA);
                if (priv != null)
                {
                    byte[] offdata = priv.getByteArray();
                    UInt32 off = ((uint)(offdata[3] << 24) | (uint)(offdata[2] << 16) |
                            (uint)(offdata[1] << 8) | (uint)offdata[0]);
                    UInt32 length = (uint)file.BaseStream.Length - off;
                    file.BaseStream.Position = off;
                    byte[] stringdata = file.ReadBytes((int)length);
                    Int32 currpos = 8;
                    while (currpos + 20 < length)
                    {
                        UInt32 tag = (((uint)(stringdata[currpos]) << 24) | ((uint)(stringdata[currpos + 1]) << 16) | ((uint)(stringdata[currpos + 2]) << 8) |
                            (uint)(stringdata[currpos + 3]));
                        UInt32 len = ((uint)(stringdata[currpos + 4 + 3] << 24) | (uint)(stringdata[currpos + 4 + 2] << 16) |
                            (uint)(stringdata[currpos + 4 + 1] << 8) | (uint)stringdata[currpos + 4]);

                        if (tag == 0x574247)
                        { /* WBG */
                            UInt16[] tmp = new UInt16[4];
                            for (UInt32 i = 0; i < 4; i++)
                                tmp[i] = (ushort)(((int)stringdata[(currpos + 12 + i * 2) + 1] << 8) | (int)stringdata[currpos + 12 + i * 2]);

                            mRaw.metadata.wbCoeffs[0] = (float)tmp[0];
                            mRaw.metadata.wbCoeffs[1] = (float)tmp[1];
                            mRaw.metadata.wbCoeffs[2] = (float)tmp[3];
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
                    mRaw.errors.Add(e.Message);
                    // We caught an exception reading WB, just ignore it
                }
            }
        }

        unsafe void SonyDecrypt(byte[] ifpData, UInt32 len, UInt32 key)
        {
            fixed (byte* temp = ifpData)
            {
                UInt32* buffer = (UInt32*)temp;
                UInt32* pad = stackalloc UInt32[128];

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
                        (((uint)((byte*)&pad[p])[2]) << 8) | ((uint)((byte*)&pad[p])[3]));
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
            Tag priv = rootIFD.getEntryRecursive(TagType.DNGPRIVATEDATA);
            if (priv != null)
            {
                byte[] data = priv.getByteArray();
                UInt32 off = ((((uint)(data)[3]) << 24) | (((uint)(data)[2]) << 16) | (((uint)(data)[1]) << 8) | ((uint)(data)[0]));
                IFD sony_private;
                sony_private = new IFD(file, off, rootIFD.endian);

                Tag sony_offset = sony_private.getEntryRecursive(TagType.SONY_OFFSET);
                Tag sony_length = sony_private.getEntryRecursive(TagType.SONY_LENGTH);
                Tag sony_key = sony_private.getEntryRecursive(TagType.SONY_KEY);
                if (sony_offset == null || sony_length == null || sony_key == null || sony_key.dataCount != 4)
                    throw new RawDecoderException("ARW: couldn't find the correct metadata for WB decoding");

                off = sony_offset.getUInt();
                UInt32 len = sony_length.getUInt();
                data = sony_key.getByteArray();
                UInt32 key = ((((uint)(data)[3]) << 24) | (((uint)(data)[2]) << 16) | (((uint)(data)[1]) << 8) | ((uint)(data)[0]));
                file.BaseStream.Position = off;
                byte[] ifp_data = file.ReadBytes((int)len);


                SonyDecrypt(ifp_data, len / 4, key);

                sony_private = new IFD(new TIFFBinaryReader(TIFFBinaryReader.streamFromArray(ifp_data)), 0, rootIFD.endian, 0, (int)off);

                if (sony_private.hasEntry(TagType.SONYGRBGLEVELS))
                {
                    Tag wb = sony_private.getEntry(TagType.SONYGRBGLEVELS);
                    if (wb.dataCount != 4)
                        throw new RawDecoderException("ARW: WB has " + wb.dataCount + " entries instead of 4");
                    mRaw.metadata.wbCoeffs[0] = wb.getFloat(1) / wb.getFloat(0);
                    mRaw.metadata.wbCoeffs[1] = wb.getFloat(0) / wb.getFloat(0);
                    mRaw.metadata.wbCoeffs[2] = wb.getFloat(2) / wb.getFloat(0);
                }
                else if (sony_private.hasEntry(TagType.SONYRGGBLEVELS))
                {
                    Tag wb = sony_private.getEntry(TagType.SONYRGGBLEVELS);
                    if (wb.dataCount != 4)
                        throw new RawDecoderException("ARW: WB has " + wb.dataCount + " entries instead of 4");
                    mRaw.metadata.wbCoeffs[0] = wb.getFloat(0) / wb.getFloat(1);
                    mRaw.metadata.wbCoeffs[1] = wb.getFloat(1) / wb.getFloat(1);
                    mRaw.metadata.wbCoeffs[2] = wb.getFloat(3) / wb.getFloat(1);

                }
            }
        }
    }
}

