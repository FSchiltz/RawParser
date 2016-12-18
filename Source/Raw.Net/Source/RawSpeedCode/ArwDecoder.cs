namespace RawSpeed
{

    class ArwDecoder : RawDecoder
    {
        protected void DecodeARW(ref ByteStream input, UInt32 w, UInt32 h);
        void DecodeARW2(ref ByteStream input, UInt32 w, UInt32 h, UInt32 bpp);
        void DecodeUncompressed(ref TiffIFD[] raw);
        void SonyDecrypt(ref UInt32[] buffer, UInt32 len, UInt32 key);
        void GetWB();
        TiffIFD mRootIFD;
        ByteStream in;
		int mShiftDownScale;

        ArwDecoder(TiffIFD* rootIFD, FileMap* file) :   RawDecoder(file), mRootIFD(rootIFD)
        {
            mShiftDownScale = 0;
            decoderVersion = 1;
        }

        RawImage decodeRawInternal()
        {
            TiffIFD* raw = null;
            vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(STRIPOFFSETS);

            if (data.empty())
            {
                TiffEntry* model = mRootIFD.getEntryRecursive(MODEL);

                if (model && model.getString() == "DSLR-A100")
                {
                    // We've caught the elusive A100 in the wild, a transitional format
                    // between the simple sanity of the MRW custom format and the wordly
                    // wonderfullness of the Tiff-based ARW format, let's shoot from the hip
                    data = mRootIFD.getIFDsWithTag(SUBIFDS);
                    if (data.empty())
                        ThrowRDE("ARW: A100 format, couldn't find offset");
                    raw = data[0];
                    UInt32 off = raw.getEntry(SUBIFDS).getInt();
                    UInt32 width = 3881;
                    UInt32 height = 2608;

                    mRaw.dim = iPoint2D(width, height);
                    mRaw.createData();
                    ByteStream input(mFile, off);

                    try
                    {
                        DecodeARW(input, width, height);
                    }
                    catch (IOException e)
                    {
                        mRaw.setError(e.what());
                        // Let's ignore it, it may have delivered somewhat useful data.
                    }

                    return mRaw;
                }
                else if (hints.find("srf_format") != hints.end())
                {
                    data = mRootIFD.getIFDsWithTag(IMAGEWIDTH);
                    if (data.empty())
                        ThrowRDE("ARW: SRF format, couldn't find width/height");
                    raw = data[0];

                    UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
                    UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
                    UInt32 len = width * height * 2;

                    // Constants taken from dcraw
                    UInt32 off = 862144;
                    UInt32 key_off = 200896;
                    UInt32 head_off = 164600;

                    // Replicate the dcraw contortions to get the "decryption" key
                    byte[] data = mFile.getData(key_off, 1);
                    UInt32 offset = (*data) * 4;
                    data = mFile.getData(key_off + offset, 4);
                    UInt32 key = get4BE(data, 0);
                    byte[] head = mFile.getDataWrt(head_off, 40);
                    SonyDecrypt((UInt32*)head, 10, key);
                    for (int i = 26; i-- > 22;)
                        key = key << 8 | head[i];

                    // "Decrypt" the whole image buffer in place
                    byte[] image_data = mFile.getDataWrt(off, len);
                    SonyDecrypt((UInt32*)image_data, len / 4, key);

                    // And now decode as a normal 16bit raw
                    mRaw.dim = iPoint2D(width, height);
                    mRaw.createData();
                    ByteStream input(image_data, len);
                    Decode16BitRawBEunpacked(input, width, height);

                    return mRaw;
                }
                else
                {
                    ThrowRDE("ARW Decoder: No image data found");
                }
            }

            raw = data[0];
            int compression = raw.getEntry(COMPRESSION).getInt();
            if (1 == compression)
            {
                try
                {
                    DecodeUncompressed(raw);
                }
                catch (IOException &e) {
                    mRaw.setError(e.what());
                }

                return mRaw;
                }

                if (32767 != compression)
                    ThrowRDE("ARW Decoder: Unsupported compression");

                TiffEntry* offsets = raw.getEntry(STRIPOFFSETS);
                TiffEntry* counts = raw.getEntry(STRIPBYTECOUNTS);

                if (offsets.count != 1)
                {
                    ThrowRDE("ARW Decoder: Multiple Strips found: %u", offsets.count);
                }
                if (counts.count != offsets.count)
                {
                    ThrowRDE("ARW Decoder: Byte count number does not match strip size: count:%u, strips:%u ", counts.count, offsets.count);
                }
                UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
                UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
                UInt32 bitPerPixel = raw.getEntry(BITSPERSAMPLE).getInt();

                // Sony E-550 marks compressed 8bpp ARW with 12 bit per pixel
                // this makes the compression detect it as a ARW v1.
                // This camera has however another MAKER entry, so we MAY be able
                // to detect it this way in the future.
                data = mRootIFD.getIFDsWithTag(MAKE);
                if (data.size() > 1)
                {
                    for (UInt32 i = 0; i < data.size(); i++)
                    {
                        string make = data[i].getEntry(MAKE).getString();
                        /* Check for maker "SONY" without spaces */
                        if (!make.compare("SONY"))
                            bitPerPixel = 8;
                    }
                }

                bool arw1 = counts.getInt() * 8 != width * height * bitPerPixel;
                if (arw1)
                    height += 8;

                mRaw.dim = iPoint2D(width, height);
                mRaw.createData();

                UInt16 curve[0x4001];
                TiffEntry* c = raw.getEntry(SONY_CURVE);
                UInt32 sony_curve[] = { 0, 0, 0, 0, 0, 4095 };

                for (UInt32 i = 0; i < 4; i++)
                    sony_curve[i + 1] = (c.getShort(i) >> 2) & 0xfff;

                for (UInt32 i = 0; i < 0x4001; i++)
                    curve[i] = i;

                for (UInt32 i = 0; i < 5; i++)
                    for (UInt32 j = sony_curve[i] + 1; j <= sony_curve[i + 1]; j++)
                        curve[j] = curve[j - 1] + (1 << i);

                if (!uncorrectedRawValues)
                    mRaw.setTable(curve, 0x4000, true);

                UInt32 c2 = counts.getInt();
                UInt32 off = offsets.getInt();

                if (!mFile.isValid(off))
                    ThrowRDE("Sony ARW decoder: Data offset after EOF, file probably truncated");

                if (!mFile.isValid(off, c2))
                    c2 = mFile.getSize() - off;

                ByteStream input(mFile, off, c2);

                try
                {
                    if (arw1)
                        DecodeARW(input, width, height);
                    else
                        DecodeARW2(input, width, height, bitPerPixel);
                }
                catch (IOException &e) {
                    mRaw.setError(e.what());
                    // Let's ignore it, it may have delivered somewhat useful data.
                }

                // Set the table, if it should be needed later.
                if (uncorrectedRawValues)
                {
                    mRaw.setTable(curve, 0x4000, false);
                }
                else
                {
                    mRaw.setTable(null);
                }

                return mRaw;
                }

                void DecodeUncompressed(TiffIFD* raw)
                {
                    UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
                    UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
                    UInt32 off = raw.getEntry(STRIPOFFSETS).getInt();
                    UInt32 c2 = raw.getEntry(STRIPBYTECOUNTS).getInt();

                    mRaw.dim = iPoint2D(width, height);
                    mRaw.createData();
                    ByteStream input(mFile, off, c2);

                    if (hints.find("sr2_format") != hints.end())
                        Decode14BitRawBEunpacked(input, width, height);
                    else
                        Decode16BitRawUnpacked(input, width, height);
                }

                void DecodeARW(ref ByteStream input, UInt32 w, UInt32 h)
                {
                    BitPumpMSB bits(ref input);
                    byte[] data = mRaw.getData();
                    UInt16* dest = (UInt16*)&data[0];
                    UInt32 pitch = mRaw.pitch / sizeof(UInt16);
                    int sum = 0;
                    for (UInt32 x = w; x--;)
                        for (UInt32 y = 0; y < h + 1; y += 2)
                        {
                            bits.checkPos();
                            bits.fill();
                            if (y == h) y = 1;
                            UInt32 len = 4 - bits.getBitsNoFill(2);
                            if (len == 3 && bits.getBitNoFill()) len = 0;
                            if (len == 4)
                                while (len < 17 && !bits.getBitNoFill()) len++;
                            int diff = bits.getBits(len);
                            if (len && (diff & (1 << (len - 1))) == 0)
                                diff -= (1 << len) - 1;
                            sum += diff;
                            _ASSERTE(!(sum >> 12));
                            if (y < h) dest[x + y * pitch] = sum;
                        }
                }

                void DecodeARW2(ref ByteStream input, UInt32 w, UInt32 h, UInt32 bpp)
                {

                    if (bpp == 8)
                    {
					in = &input;
                        this.startThreads();
                        return;
                    } // End bpp = 8

                    if (bpp == 12)
                    {
                        byte[] data = mRaw.getData();
                        UInt32 pitch = mRaw.pitch;
                        byte8*in = input.getData();

                        if (input.getRemainSize() < (w * 3 / 2))
                            ThrowRDE("Sony Decoder: Image data section too small, file probably truncated");

                        if (input.getRemainSize() < (w * h * 3 / 2))
                            h = input.getRemainSize() / (w * 3 / 2) - 1;

                        for (UInt32 y = 0; y < h; y++)
                        {
                            UInt16* dest = (UInt16*)&data[y * pitch];
                            for (UInt32 x = 0; x < w; x += 2)
                            {
                                UInt32 g1 = *in++;
                                UInt32 g2 = *in++;
                                dest[x] = (g1 | ((g2 & 0xf) << 8));
                                UInt32 g3 = *in++;
                                dest[x + 1] = ((g2 >> 4) | (g3 << 4));
                            }
                        }
                        // Shift scales, since black and white are the same as compressed precision
                        mShiftDownScale = 2;
                        return;
                    }
                    ThrowRDE("Unsupported bit depth");
                }

                void checkSupportInternal(CameraMetaData* meta)
                {
                    vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
                    if (data.empty())
                        ThrowRDE("ARW Support check: Model name found");
                    string make = data[0].getEntry(MAKE).getString();
                    string model = data[0].getEntry(MODEL).getString();
                    this.checkCameraSupported(meta, make, model, "");
                }

                void decodeMetaDataInternal(CameraMetaData* meta)
                {
                    //Default
                    int iso = 0;

                    mRaw.cfa.setCFA(iPoint2D(2, 2), CFA_RED, CFA_GREEN, CFA_GREEN2, CFA_BLUE);
                    vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);

                    if (data.empty())
                        ThrowRDE("ARW Meta Decoder: Model name found");
                    if (!data[0].hasEntry(MAKE))
                        ThrowRDE("ARW Decoder: Make name not found");

                    string make = data[0].getEntry(MAKE).getString();
                    string model = data[0].getEntry(MODEL).getString();

                    if (mRootIFD.hasEntryRecursive(ISOSPEEDRATINGS))
                        iso = mRootIFD.getEntryRecursive(ISOSPEEDRATINGS).getInt();

                    setMetaData(meta, make, model, "", iso);
                    mRaw.whitePoint >>= mShiftDownScale;
                    mRaw.blackLevel >>= mShiftDownScale;

                    // Set the whitebalance
                    if (model == "DSLR-A100")
                    { // Handle the MRW style WB of the A100
                        if (mRootIFD.hasEntryRecursive(DNGPRIVATEDATA))
                        {
                            TiffEntry* priv = mRootIFD.getEntryRecursive(DNGPRIVATEDATA);
                            byte[] offdata = priv.getData();
                            UInt32 off = get4LE(offdata, 0);
                            UInt32 length = mFile.getSize() - off;
                            unsigned stringdata = mFile.getData(off, length);
                            UInt32 currpos = 8;
                            while (currpos + 20 < length)
                            {
                                UInt32 tag = get4BE(data, currpos);
                                UInt32 len = get4LE(data, currpos + 4);
                                if (tag == 0x574247)
                                { /* WBG */
                                    UInt16 tmp[4];
                                    for (UInt32 i = 0; i < 4; i++)
                                        tmp[i] = get2LE(data, currpos + 12 + i * 2);

                                    mRaw.metadata.wbCoeffs[0] = (float)tmp[0];
                                    mRaw.metadata.wbCoeffs[1] = (float)tmp[1];
                                    mRaw.metadata.wbCoeffs[2] = (float)tmp[3];
                                    break;
                                }
                                currpos += Math.Max(len + 8, 1); // Math.Max(,1) to make sure we make progress
                            }
                        }
                    }
                    else
                    { // Everything else but the A100
                        try
                        {
                            GetWB();
                        }
                        catch (exception&e) {
                    mRaw.setError(e.what());
                    // We caught an exception reading WB, just ignore it
                }
            }
        }

        void SonyDecrypt(UInt32* buffer, UInt32 len, UInt32 key)
        {
            UInt32 pad[128];

            // Initialize the decryption pad from the key
            for (int p = 0; p < 4; p++)
                pad[p] = key = key * 48828125 + 1;
            pad[3] = pad[3] << 1 | (pad[0] ^ pad[2]) >> 31;
            for (int p = 4; p < 127; p++)
                pad[p] = (pad[p - 4] ^ pad[p - 2]) << 1 | (pad[p - 3] ^ pad[p - 1]) >> 31;
            for (int p = 0; p < 127; p++)
                pad[p] = get4BE((byte8*)&pad[p], 0);

            int p = 127;
            // Decrypt the buffer in place using the pad
            while (len--)
            {
                pad[p & 127] = pad[(p + 1) & 127] ^ pad[(p + 1 + 64) & 127];
                *buffer++ ^= pad[p & 127];
                p++;
            }
        }

        void GetWB()
        {
            // Set the whitebalance for all the modern ARW formats (everything after A100)
            if (mRootIFD.hasEntryRecursive(DNGPRIVATEDATA))
            {
                TiffEntry* priv = mRootIFD.getEntryRecursive(DNGPRIVATEDATA);
                byte[] data = priv.getData();
                UInt32 off = get4LE(data, 0);
                TiffIFD* sony_private;
                if (mRootIFD.endian == getHostEndianness())
                    sony_private = new TiffIFD(mFile, off);
                else
                    sony_private = new TiffIFDBE(mFile, off);

                TiffEntry* sony_offset = sony_private.getEntryRecursive(SONY_OFFSET);
                TiffEntry* sony_length = sony_private.getEntryRecursive(SONY_LENGTH);
                TiffEntry* sony_key = sony_private.getEntryRecursive(SONY_KEY);
                if (!sony_offset || !sony_length || !sony_key || sony_key.count != 4)
                    ThrowRDE("ARW: couldn't find the correct metadata for WB decoding");

                off = sony_offset.getInt();
                UInt32 len = sony_length.getInt();
                data = sony_key.getData();
                UInt32 key = get4LE(data, 0);

                if (sony_private)
                    delete(sony_private);

                UInt32* ifp_data = (UInt32*)mFile.getDataWrt(off, len);
                SonyDecrypt(ifp_data, len / 4, key);

                if (mRootIFD.endian == Common.getHostEndianness())
                    sony_private = new TiffIFD(mFile, off);
                else
                    sony_private = new TiffIFDBE(mFile, off);

                if (sony_private.hasEntry(SONYGRBGLEVELS))
                {
                    TiffEntry* wb = sony_private.getEntry(SONYGRBGLEVELS);
                    if (wb.count != 4)
                        ThrowRDE("ARW: WB has %d entries instead of 4", wb.count);
                    mRaw.metadata.wbCoeffs[0] = wb.getFloat(1);
                    mRaw.metadata.wbCoeffs[1] = wb.getFloat(0);
                    mRaw.metadata.wbCoeffs[2] = wb.getFloat(2);
                }
                else if (sony_private.hasEntry(SONYRGGBLEVELS))
                {
                    TiffEntry* wb = sony_private.getEntry(SONYRGGBLEVELS);
                    if (wb.count != 4)
                        ThrowRDE("ARW: WB has %d entries instead of 4", wb.count);
                    mRaw.metadata.wbCoeffs[0] = wb.getFloat(0);
                    mRaw.metadata.wbCoeffs[1] = wb.getFloat(1);
                    mRaw.metadata.wbCoeffs[2] = wb.getFloat(3);
                }
                if (sony_private)
                    delete(sony_private);
            }
        }

        /* Since ARW2 compressed images have predictable offsets, we decode them threaded */

        void decodeThreaded(RawDecoderThread* t)
        {
            byte[] data = mRaw.getData();
            UInt32 pitch = mRaw.pitch;
            int32 w = mRaw.dim.x;

            BitPumpPlain bits(in);
            for (UInt32 y = t.start_y; y < t.end_y; y++)
            {
                UInt16* dest = (UInt16*)&data[y * pitch];
                // Realign
                bits.setAbsoluteOffset((w * 8 * y) >> 3);
                UInt32 random = bits.peekBits(24);

                // Process 32 pixels (16x2) per loop.
                for (int32 x = 0; x < w - 30;)
                {
                    bits.checkPos();
                    int _max = bits.getBits(11);
                    int _Math.Math.Min(( = bits.getBits(11);
                    int _imax = bits.getBits(4);
                    int _iMath.Math.Min(( = bits.getBits(4);
                    int sh;
                    for (sh = 0; sh < 4 && 0x80 << sh <= _max - _Math.Math.Min((; sh++) ;
                    for (int i = 0; i < 16; i++)
                    {
                        int p;
                        if (i == _imax) p = _max;
                        else if (i == _iMath.Math.Min(() p = _Math.Math.Min((;
                        else
                        {
                            p = (bits.getBits(7) << sh) + _Math.Math.Min((;
                            if (p > 0x7ff)
                                p = 0x7ff;
                        }
                        mRaw.setWithLookUp(p << 1, (byte8*)&dest[x + i * 2], &random);
                    }
                    x += x & 1 ? 31 : 1;  // Skip to next 32 pixels
                }
            }
        }
    }
}

