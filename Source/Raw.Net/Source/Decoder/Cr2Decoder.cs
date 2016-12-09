using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class Cr2Slice
    {
        public UInt32 w;
        public UInt32 h;
        public UInt32 offset;
        public UInt32 count;
    };

    internal class Cr2Decoder : RawDecoder
    {
        int[] sraw_coeffs = new int[3];
        IFD rootIFD;

        public Cr2Decoder(IFD rootIFD, TIFFBinaryReader file, CameraMetaData meta) : base(ref file, meta)
        {
            this.rootIFD = (rootIFD);
            decoderVersion = 6;
        }

        /**
         * Taken from nikon decoder
         */
        protected override Thumbnail decodeThumbInternal()
        {
            //find the preview ifd (ifd1 for thumb)(IFD0 is better, bigger preview buut too big and slow for now)
            IFD preview = rootIFD.getIFDsWithTag((TagType)0x0201)[0];
            //no thumbnail
            if (preview == null) return null;

            var thumb = preview.getEntry((TagType)0x0201);
            var size = preview.getEntry((TagType)0x0202);
            if (size == null || thumb == null) return null;

            
            file.Position = (uint)(thumb.data[0]) ;
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
            if (hints.ContainsKey("old_format"))
            {
                UInt32 off = 0;
                var t = rootIFD.getEntryRecursive((TagType)0x81);
                if (t != null)
                    off = t.getUInt();
                else
                {
                    List<IFD> data2 = rootIFD.getIFDsWithTag(TagType.CFAPATTERN);
                    if (data2.Count == 0)
                        throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                    else
                    {
                        if (data2[0].hasEntry(TagType.STRIPOFFSETS))
                            off = data2[0].getEntry(TagType.STRIPOFFSETS).getUInt();
                        else
                            throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                    }
                }

                var b = new TIFFBinaryReader(file.BaseStream, off + 41, (uint)file.BaseStream.Length);

                UInt32 height = (uint)b.ReadInt16();
                UInt32 width = (uint)b.ReadInt16();

                // Every two lines can be encoded as a single line, probably to try and get
                // better compression by getting the same RGBG sequence in every line
                if (hints.ContainsKey("double_line_ljpeg"))
                {
                    height *= 2;
                    mRaw.dim = new Point2D((int)width * 2, (int)height / 2);
                }
                else
                {
                    width *= 2;
                    mRaw.dim = new Point2D((int)width, (int)height);
                }

                mRaw.Init();
                LJpegPlain l = new LJpegPlain(file, mRaw);
                try
                {
                    l.startDecoder(off, (uint)(file.BaseStream.Length - off), 0, 0);
                }
                catch (IOException e)
                {
                    mRaw.errors.Add(e.Message);
                }

                if (hints.ContainsKey("double_line_ljpeg"))
                {
                    // We now have a double width half height image we need to convert to the
                    // normal format
                    var final_size = new Point2D((int)width, (int)height);
                    RawImage procRaw = new RawImage();
                    procRaw.dim = final_size;
                    procRaw.Init();
                    procRaw.metadata = mRaw.metadata;
                    //procRaw.copyErrorsFrom(mRaw);

                    for (UInt32 y = 0; y < height; y++)
                    {
                        throw new NotImplementedException();
                        /*
                        UInt16* dst = (UInt16*)procRaw.getData(0, y);
                        UInt16* src = (UInt16*)mRaw.getData(y % 2 == 0 ? 0 : width, y / 2);
                        for (UInt32 x = 0; x < width; x++)
                            dst[x] = src[x];*/
                    }
                    mRaw = procRaw;
                }

                var tv = rootIFD.getEntryRecursive((TagType)0x123);
                if (tv != null)
                {
                    Tag curve = rootIFD.getEntryRecursive((TagType)0x123);
                    if (curve.dataType == TiffDataType.SHORT && curve.dataCount == 4096)
                    {
                        Tag linearization = rootIFD.getEntryRecursive((TagType)0x123);
                        UInt32 len = linearization.dataCount;
                        linearization.getShortArray(out var table, (int)len);

                        mRaw.setTable(table, 4096, true);
                        // Apply table
                        //mRaw.sixteenBitLookup();
                        // Delete table
                        // mRaw.setTable(null);

                    }
                }

                return mRaw;
            }

            List<IFD> data = rootIFD.getIFDsWithTag((TagType)0xc5d8);

            if (data.Count == 0)
                throw new RawDecoderException("CR2 Decoder: No image data found");


            IFD raw = data[0];
            mRaw = new RawImage();
            mRaw.isCFA = true;
            List<Cr2Slice> slices = new List<Cr2Slice>();
            int completeH = 0;
            bool doubleHeight = false;
            mRaw.ColorDepth = 14;
            try
            {
                Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
                // Iterate through all slices
                for (UInt32 s = 0; s < offsets.dataCount; s++)
                {
                    Cr2Slice slice = new Cr2Slice();
                    slice.offset = Convert.ToUInt32(offsets.data[s]);
                    slice.count = Convert.ToUInt32(counts.data[s]);
                    SOFInfo sof = new SOFInfo();
                    LJpegPlain l = new LJpegPlain(file, mRaw);
                    l.getSOF(ref sof, slice.offset, slice.count);
                    slice.w = sof.w * sof.cps;
                    slice.h = sof.h;
                    if (sof.cps == 4 && slice.w > slice.h * 4)
                    {
                        doubleHeight = true;
                    }
                    if (slices.Count != 0)
                        if (slices[0].w != slice.w)
                            throw new RawDecoderException("CR2 Decoder: Slice width does not match.");

                    if (file.isValid(slice.offset, slice.count)) // Only decode if size is valid
                        slices.Add(slice);
                    completeH += (int)slice.h;
                }
            }
            catch (TiffParserException)
            {
                throw new RawDecoderException("CR2 Decoder: Unsupported format.");
            }

            // Override with canon_double_height if set.
            hints.TryGetValue("canon_double_height", out var str);
            if (str != null)
                doubleHeight = (str == "true");

            if (slices.Count == 0)
            {
                throw new RawDecoderException("CR2 Decoder: No Slices found.");
            }
            mRaw.dim = new Point2D((int)slices[0].w, completeH);

            // Fix for Canon 6D mRaw, which has flipped width & height for some part of the image
            // In that case, we swap width and height, since this is the correct dimension
            bool flipDims = false;
            bool wrappedCr2Slices = false;
            if (raw.hasEntry((TagType)0xc6c5))
            {
                UInt16 ss = raw.getEntry((TagType)0xc6c5).getUShort();
                // sRaw
                if (ss == 4)
                {
                    mRaw.dim.x /= 3;
                    mRaw.cpp = (3);
                    mRaw.isCFA = false;
                    // Fix for Canon 80D mraw format.
                    // In that format, the frame (as read by getSOF()) is 4032x3402, while the
                    // real image should be 4536x3024 (where the full vertical slices in
                    // the frame "wrap around" the image.
                    if (hints.ContainsKey("wrapped_cr2_slices") && raw.hasEntry(TagType.IMAGEWIDTH) && raw.hasEntry(TagType.IMAGELENGTH))
                    {
                        wrappedCr2Slices = true;
                        int w = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                        int h = raw.getEntry(TagType.IMAGELENGTH).getInt();
                        if (w * h != mRaw.dim.x * mRaw.dim.y)
                        {
                            throw new RawDecoderException("CR2 Decoder: Wrapped slices don't match image size");
                        }
                        mRaw.dim = new Point2D(w, h);
                    }
                }
                flipDims = mRaw.dim.x < mRaw.dim.y;
                if (flipDims)
                {
                    int w = mRaw.dim.x;
                    mRaw.dim.x = mRaw.dim.y;
                    mRaw.dim.y = w;
                }
            }

            mRaw.Init();

            List<int> s_width = new List<int>();
            if (raw.hasEntry(TagType.CANONCR2SLICE))
            {
                Tag ss = raw.getEntry(TagType.CANONCR2SLICE);
                for (int i = 0; i < ss.getShort(0); i++)
                {
                    s_width.Add(ss.getShort(1));
                }
                s_width.Add(ss.getShort(2));
            }
            else
            {
                s_width.Add((int)slices[0].w);
            }
            UInt32 offY = 0;

            if (s_width.Count > 15)
                throw new RawDecoderException("CR2 Decoder: No more than 15 slices supported");
            //_RPT1(0, "Org slices:%d\n", s_width.size());
            for (int i = 0; i < slices.Count; i++)
            {
                Cr2Slice slice = slices[i];
                try
                {
                    LJpegPlain l = new LJpegPlain(file, mRaw);
                    l.addSlices(s_width);
                    l.mUseBigtable = true;
                    l.mCanonFlipDim = flipDims;
                    l.mCanonDoubleHeight = doubleHeight;
                    l.mWrappedCr2Slices = wrappedCr2Slices;
                    l.startDecoder(slice.offset, slice.count, 0, offY);
                }
                catch (RawDecoderException e)
                {
                    if (i == 0)
                        throw new Exception();
                    // These may just be single slice error - store the error and move on
                    mRaw.errors.Add(e.Message);
                }
                catch (IOException e)
                {
                    // Let's try to ignore this - it might be truncated data, so something might be useful.
                    mRaw.errors.Add(e.Message);
                }
                offY += slice.w;
            }

            /*
            if (mRaw.metadata.subsampling.x > 1 || mRaw.metadata.subsampling.y > 1)
                sRawInterpolate();*/

            return mRaw;
        }

        protected override void checkSupportInternal()
        {
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("CR2 Support check: Model name not found");
            if (!data[0].hasEntry(TagType.MAKE))
                throw new RawDecoderException("CR2 Support: Make name not found");
            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;

            // Check for sRaw mode
            data = rootIFD.getIFDsWithTag((TagType)0xc5d8);
            if (data.Count != 0)
            {
                IFD raw = data[0];
                if (raw.hasEntry((TagType)0xc6c5))
                {
                    UInt16 ss = raw.getEntry((TagType)0xc6c5).getUShort();
                    if (ss == 4)
                    {
                        this.checkCameraSupported(metaData, make, model, "sRaw1");
                        return;
                    }
                }
            }
            this.checkCameraSupported(metaData, make, model, "");
        }

        protected override void decodeMetaDataInternal()
        {
            int iso = 0;
            mRaw.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            List<IFD> data = rootIFD.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("CR2 Meta Decoder: Model name not found");

            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;
            string mode = "";

            if (mRaw.metadata.subsampling.y == 2 && mRaw.metadata.subsampling.x == 2)
                mode = "sRaw1";

            if (mRaw.metadata.subsampling.y == 1 && mRaw.metadata.subsampling.x == 2)
                mode = "sRaw2";
            var isoTag = rootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                iso = isoTag.getInt();

            // Fetch the white balance
            try
            {
                Tag wb = rootIFD.getEntryRecursive(TagType.CANONCOLORDATA);
                if (wb != null)
                {
                    // this entry is a big table, and different cameras store used WB in
                    // different parts, so find the offset, starting with the most common one
                    int offset = 126;

                    hints.TryGetValue("wb_offset", out var s);
                    // replace it with a hint if it exists
                    if (s != null)
                    {

                        offset = Int32.Parse(s);

                    }

                    offset /= 2;
                    mRaw.metadata.wbCoeffs[0] = Convert.ToSingle(wb.data[offset + 0]);
                    mRaw.metadata.wbCoeffs[1] = Convert.ToSingle(wb.data[offset + 1]);
                    mRaw.metadata.wbCoeffs[2] = Convert.ToSingle(wb.data[offset + 3]);
                }
                else
                {
                    data = null;
                    data = rootIFD.getIFDsWithTag(TagType.MODEL);

                    Tag shot_info = rootIFD.getEntryRecursive(TagType.CANONSHOTINFO);
                    Tag g9_wb = rootIFD.getEntryRecursive(TagType.CANONPOWERSHOTG9WB);
                    if (shot_info != null && g9_wb != null)
                    {
                        UInt16 wb_index = Convert.ToUInt16(shot_info.data[7]);
                        int wb_offset = (wb_index < 18) ? "012347800000005896"[wb_index] - '0' : 0;
                        wb_offset = wb_offset * 8 + 2;

                        mRaw.metadata.wbCoeffs[0] = (float)g9_wb.getInt(wb_offset + 1);
                        mRaw.metadata.wbCoeffs[1] = ((float)g9_wb.getInt(wb_offset + 0) + (float)g9_wb.getInt(wb_offset + 3)) / 2.0f;
                        mRaw.metadata.wbCoeffs[2] = (float)g9_wb.getInt(wb_offset + 2);
                    }
                    else
                    {
                        // WB for the old 1D and 1DS
                        wb = null;
                        wb = rootIFD.getEntryRecursive((TagType)0xa4);
                        if (wb != null)
                        {
                            if (wb.dataCount >= 3)
                            {
                                mRaw.metadata.wbCoeffs[0] = wb.getFloat(0);
                                mRaw.metadata.wbCoeffs[1] = wb.getFloat(1);
                                mRaw.metadata.wbCoeffs[2] = wb.getFloat(2);
                            }
                        }
                    }                  
                }
            }
            catch (Exception e)
            {
                mRaw.errors.Add(e.Message);
                // We caught an exception reading WB, just ignore it
            }
            setMetaData(metaData, make, model, mode, iso);

            mRaw.metadata.wbCoeffs[0] = mRaw.metadata.wbCoeffs[0] / mRaw.metadata.wbCoeffs[1];
            mRaw.metadata.wbCoeffs[1] = mRaw.metadata.wbCoeffs[1] / mRaw.metadata.wbCoeffs[1];
            mRaw.metadata.wbCoeffs[2] = mRaw.metadata.wbCoeffs[2] / mRaw.metadata.wbCoeffs[1];
        }

        int getHue()
        {
            if (hints.ContainsKey("old_sraw_hue"))
                return (mRaw.metadata.subsampling.y * mRaw.metadata.subsampling.x);
            var tc = rootIFD.getEntryRecursive((TagType)0x10);
            if (tc == null)
            {
                return 0;
            }
            UInt32 model_id = rootIFD.getEntryRecursive((TagType)0x10).getUInt();
            if (model_id >= 0x80000281 || model_id == 0x80000218 || (hints.ContainsKey("force_new_sraw_hue")))
                return ((mRaw.metadata.subsampling.y * mRaw.metadata.subsampling.x) - 1) >> 1;

            return (mRaw.metadata.subsampling.y * mRaw.metadata.subsampling.x);
        }

        /*
        // Interpolate and convert sRaw data.
        void sRawInterpolate()
        {
            List<IFD> data = mRootIFD.getIFDsWithTag(CANONCOLORDATA);
            if (data.Count == 0)
                throw new RawDecoderException("CR2 sRaw: Unable to locate WB info.");

            Tag wb = data[0].getEntry(CANONCOLORDATA);
            // Offset to sRaw coefficients used to reconstruct uncorrected RGB data.
            UInt32 offset = 78;

            sraw_coeffs[0] = wb.getShort(offset + 0);
            sraw_coeffs[1] = (wb.getShort(offset + 1) + wb.getShort(offset + 2) + 1) >> 1;
            sraw_coeffs[2] = wb.getShort(offset + 3);

            if (hints.ContainsKey("invert_sraw_wb"))
            {
                sraw_coeffs[0] = (int)(1024.0f / ((float)sraw_coeffs[0] / 1024.0f));
                sraw_coeffs[2] = (int)(1024.0f / ((float)sraw_coeffs[2] / 1024.0f));
            }

            // Determine sRaw coefficients 
            bool isOldSraw = hints.ContainsKey("sraw_40d");
            bool isNewSraw = hints.ContainsKey("sraw_new");

            if (mRaw.metadata.subsampling.y == 1 && mRaw.metadata.subsampling.x == 2)
            {
                if (isOldSraw)
                    interpolate_422_old(mRaw.dim.x / 2, mRaw.dim.y, 0, mRaw.dim.y);
                else if (isNewSraw)
                    interpolate_422_new(mRaw.dim.x / 2, mRaw.dim.y, 0, mRaw.dim.y);
                else
                    interpolate_422(mRaw.dim.x / 2, mRaw.dim.y, 0, mRaw.dim.y);
            }
            else if (mRaw.metadata.subsampling.y == 2 && mRaw.metadata.subsampling.x == 2)
            {
                if (isNewSraw)
                    interpolate_420_new(mRaw.dim.x / 2, mRaw.dim.y / 2, 0, mRaw.dim.y / 2);
                else
                    interpolate_420(mRaw.dim.x / 2, mRaw.dim.y / 2, 0, mRaw.dim.y / 2);
            }
            else
                throw new RawDecoderException("CR2 Decoder: Unknown subsampling");
        }

#define YUV_TO_RGB(Y, Cb, Cr) r = sraw_coeffs[0] * ((int)Y + (( 50*(int)Cb + 22929*(int)Cr) >> 12));\
        g = sraw_coeffs[1] * ((int) Y + ((-5640 * (int) Cb - 11751 * (int) Cr) >> 12));\
  b = sraw_coeffs[2] * ((int) Y + ((29040 * (int) Cb - 101 * (int) Cr) >> 12));\
  r >>= 8; g >>= 8; b >>= 8;

void STORE_RGB(X, A, B, C)
        {
            X[A] = Common.clampbits(r, 16); X[B] = Common.clampbits(g, 16); X[C] = Common.clampbits(b, 16);
        }
        */

        /*
    // sRaw interpolators - ugly as sin, but does the job in reasonably speed
    // Note: Thread safe.
    void interpolate_422(int w, int h, int start_h, int end_h)
    {
        // Last pixel should not be interpolated
        w--;

        // Current line
        UInt16* c_line;
        int hue = -getHue() + 16384;
        for (int y = start_h; y < end_h; y++)
        {
            c_line = (UInt16*)mRaw.getData(0, y);
            int r, g, b;
            int off = 0;
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;

                Y = c_line[off];
                int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                YUV_TO_RGB(Y, Cb2, Cr2);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;
            }
            // Last two pixels
            int Y = c_line[off];
            int Cb = c_line[off + 1] - hue;
            int Cr = c_line[off + 2] - hue;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off, off + 1, off + 2);

            Y = c_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off + 3, off + 4, off + 5);
        }
    }


    // Note: Not thread safe, since it writes inplace.
    void interpolate_420(int w, int h, int start_h, int end_h)
    {
        // Last pixel should not be interpolated
        w--;

        bool atLastLine = false;

        if (end_h == h)
        {
            end_h--;
            atLastLine = true;
        }

        // Current line
        UInt16* c_line;
        // Next line
        UInt16* n_line;
        // Next line again
        UInt16* nn_line;

        int off;
        int r, g, b;
        int hue = -getHue() + 16384;

        for (int y = start_h; y < end_h; y++)
        {
            c_line = (UInt16*)mRaw.getData(0, y * 2);
            n_line = (UInt16*)mRaw.getData(0, y * 2 + 1);
            nn_line = (UInt16*)mRaw.getData(0, y * 2 + 2);
            off = 0;
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);

                Y = c_line[off + 3];
                int Cb2 = (Cb + c_line[off + 1 + 6] - hue) >> 1;
                int Cr2 = (Cr + c_line[off + 2 + 6] - hue) >> 1;
                YUV_TO_RGB(Y, Cb2, Cr2);
                STORE_RGB(c_line, off + 3, off + 4, off + 5);

                // Next line
                Y = n_line[off];
                int Cb3 = (Cb + nn_line[off + 1] - hue) >> 1;
                int Cr3 = (Cr + nn_line[off + 2] - hue) >> 1;
                YUV_TO_RGB(Y, Cb3, Cr3);
                STORE_RGB(n_line, off, off + 1, off + 2);

                Y = n_line[off + 3];
                Cb = (Cb + Cb2 + Cb3 + nn_line[off + 1 + 6] - hue) >> 2;  //Left + Above + Right +Below
                Cr = (Cr + Cr2 + Cr3 + nn_line[off + 2 + 6] - hue) >> 2;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off + 3, off + 4, off + 5);
                off += 6;
            }
            int Y = c_line[off];
            int Cb = c_line[off + 1] - hue;
            int Cr = c_line[off + 2] - hue;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off, off + 1, off + 2);

            Y = c_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off + 3, off + 4, off + 5);

            // Next line
            Y = n_line[off];
            Cb = (Cb + nn_line[off + 1] - hue) >> 1;
            Cr = (Cr + nn_line[off + 2] - hue) >> 1;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(n_line, off, off + 1, off + 2);

            Y = n_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(n_line, off + 3, off + 4, off + 5);
        }

        if (atLastLine)
        {
            c_line = (UInt16*)mRaw.getData(0, end_h * 2);
            n_line = (UInt16*)mRaw.getData(0, end_h * 2 + 1);
            off = 0;

            // Last line
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);

                Y = c_line[off + 3];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off + 3, off + 4, off + 5);

                // Next line
                Y = n_line[off];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off, off + 1, off + 2);

                Y = n_line[off + 3];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off + 3, off + 4, off + 5);
                off += 6;
            }
        }
    }

#undef YUV_TO_RGB

#define YUV_TO_RGB(Y, Cb, Cr) r = sraw_coeffs[0] * (Y + Cr -512 );\
    g = sraw_coeffs[1] * (Y + ((-778 * Cb - (Cr << 11)) >> 12) - 512);\
b = sraw_coeffs[2] * (Y + (Cb - 512));\
r >>= 8; g >>= 8; b >>= 8;


        // Note: Thread safe.
        void interpolate_422_old(int w, int h, int start_h, int end_h)
    {
        // Last pixel should not be interpolated
        w--;

        // Current line
        UInt16* c_line;
        int hue = -getHue() + 16384;

        for (int y = start_h; y < end_h; y++)
        {
            c_line = (UInt16*)mRaw.getData(0, y);
            int r, g, b;
            int off = 0;
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;

                Y = c_line[off];
                int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                YUV_TO_RGB(Y, Cb2, Cr2);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;
            }
            // Last two pixels
            int Y = c_line[off];
            int Cb = c_line[off + 1] - 16384;
            int Cr = c_line[off + 2] - 16384;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off, off + 1, off + 2);

            Y = c_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off + 3, off + 4, off + 5);
        }
    }
    */

        /*
    // Algorithm found in EOS 5d Mk III

#undef YUV_TO_RGB

#define YUV_TO_RGB(Y, Cb, Cr) r = sraw_coeffs[0] * (Y + Cr);\
    g = sraw_coeffs[1] * (Y + ((-778 * Cb - (Cr << 11)) >> 12));\
b = sraw_coeffs[2] * (Y + Cb);\
r >>= 8; g >>= 8; b >>= 8;

        void interpolate_422_new(int w, int h, int start_h, int end_h)
    {
        // Last pixel should not be interpolated
        w--;

        // Current line
        UInt16* c_line;
        int hue = -getHue() + 16384;

        for (int y = start_h; y < end_h; y++)
        {
            c_line = (UInt16*)mRaw.getData(0, y);
            int r, g, b;
            int off = 0;
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;

                Y = c_line[off];
                int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                YUV_TO_RGB(Y, Cb2, Cr2);
                STORE_RGB(c_line, off, off + 1, off + 2);
                off += 3;
            }
            // Last two pixels
            int Y = c_line[off];
            int Cb = c_line[off + 1] - 16384;
            int Cr = c_line[off + 2] - 16384;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off, off + 1, off + 2);

            Y = c_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off + 3, off + 4, off + 5);
        }
    }


    // Note: Not thread safe, since it writes inplace.
    void interpolate_420_new(int w, int h, int start_h, int end_h)
    {
        // Last pixel should not be interpolated
        w--;

        bool atLastLine = false;

        if (end_h == h)
        {
            end_h--;
            atLastLine = true;
        }

        // Current line
        UInt16* c_line;
        // Next line
        UInt16* n_line;
        // Next line again
        UInt16* nn_line;
        int hue = -getHue() + 16384;

        int off;
        int r, g, b;

        for (int y = start_h; y < end_h; y++)
        {
            c_line = (UInt16*)mRaw.getData(0, y * 2);
            n_line = (UInt16*)mRaw.getData(0, y * 2 + 1);
            nn_line = (UInt16*)mRaw.getData(0, y * 2 + 2);
            off = 0;
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);

                Y = c_line[off + 3];
                int Cb2 = (Cb + c_line[off + 1 + 6] - hue) >> 1;
                int Cr2 = (Cr + c_line[off + 2 + 6] - hue) >> 1;
                YUV_TO_RGB(Y, Cb2, Cr2);
                STORE_RGB(c_line, off + 3, off + 4, off + 5);

                // Next line
                Y = n_line[off];
                int Cb3 = (Cb + nn_line[off + 1] - hue) >> 1;
                int Cr3 = (Cr + nn_line[off + 2] - hue) >> 1;
                YUV_TO_RGB(Y, Cb3, Cr3);
                STORE_RGB(n_line, off, off + 1, off + 2);

                Y = n_line[off + 3];
                Cb = (Cb + Cb2 + Cb3 + nn_line[off + 1 + 6] - hue) >> 2;  //Left + Above + Right +Below
                Cr = (Cr + Cr2 + Cr3 + nn_line[off + 2 + 6] - hue) >> 2;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off + 3, off + 4, off + 5);
                off += 6;
            }
            int Y = c_line[off];
            int Cb = c_line[off + 1] - hue;
            int Cr = c_line[off + 2] - hue;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off, off + 1, off + 2);

            Y = c_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(c_line, off + 3, off + 4, off + 5);

            // Next line
            Y = n_line[off];
            Cb = (Cb + nn_line[off + 1] - hue) >> 1;
            Cr = (Cr + nn_line[off + 2] - hue) >> 1;
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(n_line, off, off + 1, off + 2);

            Y = n_line[off + 3];
            YUV_TO_RGB(Y, Cb, Cr);
            STORE_RGB(n_line, off + 3, off + 4, off + 5);
        }

        if (atLastLine)
        {
            c_line = (UInt16*)mRaw.getData(0, end_h * 2);
            n_line = (UInt16*)mRaw.getData(0, end_h * 2 + 1);
            off = 0;

            // Last line
            for (int x = 0; x < w; x++)
            {
                int Y = c_line[off];
                int Cb = c_line[off + 1] - hue;
                int Cr = c_line[off + 2] - hue;
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off, off + 1, off + 2);

                Y = c_line[off + 3];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(c_line, off + 3, off + 4, off + 5);

                // Next line
                Y = n_line[off];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off, off + 1, off + 2);

                Y = n_line[off + 3];
                YUV_TO_RGB(Y, Cb, Cr);
                STORE_RGB(n_line, off + 3, off + 4, off + 5);
                off += 6;
            }
        }
    }*/
    }
}
