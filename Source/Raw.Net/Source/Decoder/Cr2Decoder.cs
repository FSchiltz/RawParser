using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    internal class Cr2Decoder : TiffDecoder
    {
        int[] sraw_coeffs = new int[3];

        public Cr2Decoder(ref Stream file) : base(ref file, null)
        {
            decoderVersion = 6;
        }

        /**
         * Taken from nikon decoder
         */
        protected override Thumbnail decodeThumbInternal()
        {
            //find the preview ifd (ifd1 for thumb)(IFD0 is better, bigger preview buut too big and slow for now)
            IFD preview = ifd.getIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT)[0];
            //no thumbnail
            if (preview == null) return null;

            var thumb = preview.getEntry(TagType.JPEGINTERCHANGEFORMAT);
            var size = preview.getEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
            if (size == null || thumb == null) return null;


            reader.Position = (uint)(thumb.data[0]);
            Thumbnail temp = new Thumbnail()
            {
                data = reader.ReadBytes(Convert.ToInt32(size.data[0])),
                type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }

        protected override void decodeRawInternal()
        {
            if (hints.ContainsKey("old_format"))
            {
                UInt32 off = 0;
                var t = ifd.getEntryRecursive((TagType)0x81);
                if (t != null)
                    off = t.getUInt();
                else
                {
                    List<IFD> data2 = ifd.getIFDsWithTag(TagType.CFAPATTERN);
                    if (data2.Count == 0)
                        throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                    else
                    {
                        if (data2[0].tags.ContainsKey(TagType.STRIPOFFSETS))
                            off = data2[0].getEntry(TagType.STRIPOFFSETS).getUInt();
                        else
                            throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                    }
                }

                var b = new TIFFBinaryReader(reader.BaseStream, off + 41, (uint)reader.BaseStream.Length);

                UInt32 height = (uint)b.ReadInt16();
                UInt32 width = (uint)b.ReadInt16();

                // Every two lines can be encoded as a single line, probably to try and get
                // better compression by getting the same RGBG sequence in every line
                if (hints.ContainsKey("double_line_ljpeg"))
                {
                    height *= 2;
                    rawImage.dim = new Point2D((int)width * 2, (int)height / 2);
                }
                else
                {
                    width *= 2;
                    rawImage.dim = new Point2D((int)width, (int)height);
                }

                rawImage.Init();
                LJpegPlain l = new LJpegPlain(reader, rawImage);
                try
                {
                    l.startDecoder(off, (uint)(reader.BaseStream.Length - off), 0, 0);
                }
                catch (IOException e)
                {
                    rawImage.errors.Add(e.Message);
                }

                if (hints.ContainsKey("double_line_ljpeg"))
                {
                    // We now have a double width half height image we need to convert to the
                    // normal format
                    var final_size = new Point2D((int)width, (int)height);
                    RawImage procRaw = new RawImage()
                    {
                        dim = final_size
                    };
                    procRaw.Init();
                    procRaw.metadata = rawImage.metadata;
                    //procRaw.copyErrorsFrom(mRaw);

                    for (UInt32 y = 0; y < height; y++)
                    {
                        for (UInt32 x = 0; x < width; x++)
                            procRaw.rawData[x] = rawImage.rawData[((y % 2 == 0) ? 0 : width) + x];
                    }
                    rawImage = procRaw;
                }

                var tv = ifd.getEntryRecursive((TagType)0x123);
                if (tv != null)
                {
                    Tag curve = ifd.getEntryRecursive((TagType)0x123);
                    if (curve.dataType == TiffDataType.SHORT && curve.dataCount == 4096)
                    {
                        Tag linearization = ifd.getEntryRecursive((TagType)0x123);
                        UInt32 len = linearization.dataCount;
                        linearization.getShortArray(out var table, (int)len);

                        rawImage.setTable(table, 4096, true);
                        // Apply table
                        //mRaw.sixteenBitLookup();
                        // Delete table
                        // mRaw.setTable(null);

                    }
                }

                return;
            }

            List<IFD> data = ifd.getIFDsWithTag((TagType)0xc5d8);

            if (data.Count == 0)
                throw new RawDecoderException("CR2 Decoder: No image data found");


            IFD raw = data[0];
            rawImage = new RawImage()
            {
                isCFA = true
            };

            List<Cr2Slice> slices = new List<Cr2Slice>();
            int completeH = 0;
            bool doubleHeight = false;
            rawImage.ColorDepth = 14;
            try
            {
                Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
                // Iterate through all slices
                for (UInt32 s = 0; s < offsets.dataCount; s++)
                {
                    Cr2Slice slice = new Cr2Slice()
                    {
                        offset = Convert.ToUInt32(offsets.data[s]),
                        count = Convert.ToUInt32(counts.data[s])
                    };
                    SOFInfo sof = new SOFInfo();
                    LJpegPlain l = new LJpegPlain(reader, rawImage);
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

                    if (reader.isValid(slice.offset, slice.count)) // Only decode if size is valid
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
            rawImage.dim = new Point2D((int)slices[0].w, completeH);

            // Fix for Canon 6D mRaw, which has flipped width & height for some part of the image
            // In that case, we swap width and height, since this is the correct dimension
            bool flipDims = false;
            bool wrappedCr2Slices = false;
            if (raw.tags.ContainsKey((TagType)0xc6c5))
            {
                UInt16 ss = raw.getEntry((TagType)0xc6c5).getUShort();
                // sRaw
                if (ss == 4)
                {
                    rawImage.dim.x /= 3;
                    rawImage.cpp = (3);
                    rawImage.isCFA = false;
                    // Fix for Canon 80D mraw format.
                    // In that format, the frame (as read by getSOF()) is 4032x3402, while the
                    // real image should be 4536x3024 (where the full vertical slices in
                    // the frame "wrap around" the image.
                    if (hints.ContainsKey("wrapped_cr2_slices") && raw.tags.ContainsKey(TagType.IMAGEWIDTH) && raw.tags.ContainsKey(TagType.IMAGELENGTH))
                    {
                        wrappedCr2Slices = true;
                        int w = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                        int h = raw.getEntry(TagType.IMAGELENGTH).getInt();
                        if (w * h != rawImage.dim.x * rawImage.dim.y)
                        {
                            throw new RawDecoderException("CR2 Decoder: Wrapped slices don't match image size");
                        }
                        rawImage.dim = new Point2D(w, h);
                    }
                }
                flipDims = rawImage.dim.x < rawImage.dim.y;
                if (flipDims)
                {
                    int w = rawImage.dim.x;
                    rawImage.dim.x = rawImage.dim.y;
                    rawImage.dim.y = w;
                }
            }

            rawImage.Init();

            List<int> s_width = new List<int>();
            if (raw.tags.ContainsKey(TagType.CANONCR2SLICE))
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
                    LJpegPlain l = new LJpegPlain(reader, rawImage);
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
                    rawImage.errors.Add(e.Message);
                }
                catch (IOException e)
                {
                    // Let's try to ignore this - it might be truncated data, so something might be useful.
                    rawImage.errors.Add(e.Message);
                }
                offY += slice.w;
            }

            /*
            if (mRaw.metadata.subsampling.x > 1 || mRaw.metadata.subsampling.y > 1)
                sRawInterpolate();*/
        }

        protected override void checkSupportInternal()
        {
            /*List<IFD> data = ifd.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("CR2 Support check: Model name not found");
            if (!data[0].tags.ContainsKey(TagType.MAKE))
                throw new RawDecoderException("CR2 Support: Make name not found");
            string make = data[0].getEntry(TagType.MAKE).DataAsString;
            string model = data[0].getEntry(TagType.MODEL).DataAsString;

            // Check for sRaw mode
            data = ifd.getIFDsWithTag((TagType)0xc5d8);
            if (data.Count != 0)
            {
                IFD raw = data[0];
                if (raw.tags.ContainsKey((TagType)0xc6c5))
                {
                    UInt16 ss = raw.getEntry((TagType)0xc6c5).getUShort();
                    if (ss == 4)
                    {
                        //this.checkCameraSupported(metaData, make, model, "sRaw1");
                        return;
                    }
                }
            }
            //this.checkCameraSupported(metaData, make, model, "");*/
        }

        protected override void decodeMetaDataInternal()
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("CR2 Meta Decoder: Model name not found");

            string make = data[0].getEntry(TagType.MAKE).DataAsString;
            string model = data[0].getEntry(TagType.MODEL).DataAsString;
            string mode = "";

            if (rawImage.metadata.subsampling.y == 2 && rawImage.metadata.subsampling.x == 2)
                mode = "sRaw1";

            if (rawImage.metadata.subsampling.y == 1 && rawImage.metadata.subsampling.x == 2)
                mode = "sRaw2";

            rawImage.metadata.make = make;
            rawImage.metadata.model = model;
            rawImage.metadata.mode = mode;

            //more exifs
            var isoTag = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                rawImage.metadata.isoSpeed = isoTag.getInt();
            var exposure = ifd.getEntryRecursive(TagType.EXPOSURETIME);
            var fn = ifd.getEntryRecursive(TagType.FNUMBER);
            var t = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (t != null) rawImage.metadata.isoSpeed = t.getInt();
            if (exposure != null) rawImage.metadata.exposure = exposure.getFloat();
            if (fn != null) rawImage.metadata.aperture = fn.getFloat();

            var time = ifd.getEntryRecursive(TagType.DATETIMEORIGINAL);
            var timeModify = ifd.getEntryRecursive(TagType.DATETIMEDIGITIZED);
            if (time != null) rawImage.metadata.timeTake = time.DataAsString;
            if (timeModify != null) rawImage.metadata.timeModify = timeModify.DataAsString;

            // Fetch the white balance
            try
            {
                Tag wb = ifd.getEntryRecursive(TagType.CANONCOLORDATA);
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
                    rawImage.metadata.wbCoeffs[0] = Convert.ToSingle(wb.data[offset + 0]);
                    rawImage.metadata.wbCoeffs[1] = Convert.ToSingle(wb.data[offset + 1]);
                    rawImage.metadata.wbCoeffs[2] = Convert.ToSingle(wb.data[offset + 3]);
                }
                else
                {
                    data = null;
                    data = ifd.getIFDsWithTag(TagType.MODEL);

                    Tag shot_info = ifd.getEntryRecursive(TagType.CANONSHOTINFO);
                    Tag g9_wb = ifd.getEntryRecursive(TagType.CANONPOWERSHOTG9WB);
                    if (shot_info != null && g9_wb != null)
                    {
                        UInt16 wb_index = Convert.ToUInt16(shot_info.data[7]);
                        int wb_offset = (wb_index < 18) ? "012347800000005896"[wb_index] - '0' : 0;
                        wb_offset = wb_offset * 8 + 2;

                        rawImage.metadata.wbCoeffs[0] = (float)g9_wb.getInt(wb_offset + 1);
                        rawImage.metadata.wbCoeffs[1] = ((float)g9_wb.getInt(wb_offset + 0) + (float)g9_wb.getInt(wb_offset + 3)) / 2.0f;
                        rawImage.metadata.wbCoeffs[2] = (float)g9_wb.getInt(wb_offset + 2);
                    }
                    else
                    {
                        // WB for the old 1D and 1DS
                        wb = null;
                        wb = ifd.getEntryRecursive((TagType)0xa4);
                        if (wb != null)
                        {
                            if (wb.dataCount >= 3)
                            {
                                rawImage.metadata.wbCoeffs[0] = wb.getFloat(0);
                                rawImage.metadata.wbCoeffs[1] = wb.getFloat(1);
                                rawImage.metadata.wbCoeffs[2] = wb.getFloat(2);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                rawImage.errors.Add(e.Message);
                // We caught an exception reading WB, just ignore it
            }
            //setMetaData(metaData, make, model, mode);

            SetMetaData(model);
            //get cfa
            var cfa = ifd.getEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                Debug.WriteLine("CFA pattern is not found");
                rawImage.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.cfa.setCFA(new Point2D(2, 2), (CFAColor)cfa.getInt(0), (CFAColor)cfa.getInt(1), (CFAColor)cfa.getInt(2), (CFAColor)cfa.getInt(3));
            }

            rawImage.metadata.wbCoeffs[0] = rawImage.metadata.wbCoeffs[0] / rawImage.metadata.wbCoeffs[1];
            rawImage.metadata.wbCoeffs[2] = rawImage.metadata.wbCoeffs[2] / rawImage.metadata.wbCoeffs[1];
            rawImage.metadata.wbCoeffs[1] = rawImage.metadata.wbCoeffs[1] / rawImage.metadata.wbCoeffs[1];
        }

        override protected void SetMetaData(string model)
        {
            for (int i = 0; i < canon.GetLength(0); i++)
            {
                if (rawImage.dim.x == canon[i][0] && rawImage.dim.y == canon[i][1])
                {
                    rawImage.mOffset.x = canon[i][2];
                    rawImage.mOffset.y = canon[i][3];
                    rawImage.dim.x -= (canon[i][2]);
                    rawImage.dim.y -= (canon[i][3]);
                    //rawImage.dim.x -= canon[i][4];
                    //rawImage.dim.y -= canon[i][5];
                    /* mask[0][1] = canon[i][6];
                     mask[0][3] = -canon[i][7];
                     mask[1][1] = canon[i][8];
                     mask[1][3] = -canon[i][9];*/
                    //if (canon[i][10]) filters = canon[i][10] * 0x01010101;
                }
                /*if ((unique_id | 0x20000) == 0x2720000)
                {
                    rawImage.mOffset.x = 8;
                    rawImage.mOffset.y = 16;
                }*/
            }
            if (rawImage.ColorDepth == 15)
            {
                switch (rawImage.dim.x)
                {
                    case 3344:
                        rawImage.dim.x -= 66;
                        break;
                    case 3872:
                        rawImage.dim.x -= 72;
                        break;
                }
                if (rawImage.dim.y > rawImage.dim.x)
                {
                    //SWAP(height, width);
                    //SWAP(raw_height, raw_width);
                }
                if (rawImage.dim.x == 7200 && rawImage.dim.y == 3888)
                {
                    rawImage.dim.x = 6480;
                    rawImage.dim.y = 4320;
                }
                /*
                filters = 0;
                tiff_samples = colors = 3;
                //load_raw = &CLASS canon_sraw_load_raw;*/
            }
            else
                switch (model.Trim())
                {
                    case "PowerShot 600":
                        rawImage.dim.y = 613;
                        rawImage.dim.x = 854;
                        //raw_width = 896;
                        //colors = 4;
                        //filters = 0xe1e4e1e4;
                        //load_raw = &CLASS canon_600_load_raw;
                        break;
                    case "PowerShot A5":
                    case "PowerShot A5 Zoom":
                        rawImage.dim.y = 773;
                        rawImage.dim.x = 960;
                        //raw_width = 992;
                        rawImage.metadata.pixelAspectRatio = 256 / 235.0;
                        //filters = 0x1e4e1e4e;
                        goto canon_a5;
                        break;
                    case "PowerShot A50":
                        rawImage.dim.y = 968;
                        rawImage.dim.x = 1290;
                        //rawImage.dim.x = 1320;
                        //filters = 0x1b4e4b1e;
                        goto canon_a5;
                        break;
                    case "PowerShot Pro70":
                        rawImage.dim.y = 1024;
                        rawImage.dim.x = 1552;
                        //filters = 0x1e4b4e1b;
                        canon_a5:
                        //colors = 4;
                        rawImage.ColorDepth = 10;
                        //load_raw = &CLASS packed_load_raw;
                        //load_flags = 40;
                        break;
                    case "PowerShot Pro90 IS":
                    case "PowerShot G1":
                        //colors = 4;
                        //filters = 0xb4b4b4b4;
                        break;
                    case "PowerShot A610":
                        //if(canon_s2is()) strcpy(model + 10, "S2 IS");
                        break;
                    case "PowerShot SX220 HS":
                        //mask[1][3] = -4;
                        break;
                    case "EOS D2000C":
                        //filters = 0x61616161;
                        rawImage.blackLevel = (int)rawImage.curve[200];
                        break;
                }
        }

        int GetHue()
        {
            if (hints.ContainsKey("old_sraw_hue"))
                return (rawImage.metadata.subsampling.y * rawImage.metadata.subsampling.x);
            var tc = ifd.getEntryRecursive((TagType)0x10);
            if (tc == null)
            {
                return 0;
            }
            UInt32 model_id = ifd.getEntryRecursive((TagType)0x10).getUInt();
            if (model_id >= 0x80000281 || model_id == 0x80000218 || (hints.ContainsKey("force_new_sraw_hue")))
                return ((rawImage.metadata.subsampling.y * rawImage.metadata.subsampling.x) - 1) >> 1;

            return (rawImage.metadata.subsampling.y * rawImage.metadata.subsampling.x);
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
        private ushort[][] canon = {
      new ushort[]{ 1944, 1416,   0,  0, 48,  0 },
      new ushort[]{ 2144, 1560,   4,  8, 52,  2, 0, 0, 0, 25 },
      new ushort[]{ 2224, 1456,  48,  6,  0,  2 },
      new ushort[]{ 2376, 1728,  12,  6, 52,  2 },
      new ushort[]{ 2672, 1968,  12,  6, 44,  2 },
      new ushort[]{ 3152, 2068,  64, 12,  0,  0, 16 },
      new ushort[]{ 3160, 2344,  44, 12,  4,  4 },
      new ushort[]{ 3344, 2484,   4,  6, 52,  6 },
      new ushort[]{ 3516, 2328,  42, 14,  0,  0 },
      new ushort[]{ 3596, 2360,  74, 12,  0,  0 },
      new ushort[]{ 3744, 2784,  52, 12,  8, 12 },
      new ushort[]{ 3944, 2622,  30, 18,  6,  2 },
      new ushort[]{ 3948, 2622,  42, 18,  0,  2 },
      new ushort[]{ 3984, 2622,  76, 20,  0,  2, 14 },
      new ushort[]{ 4104, 3048,  48, 12, 24, 12 },
      new ushort[]{ 4116, 2178,   4,  2,  0,  0 },
      new ushort[]{ 4152, 2772, 192, 12,  0,  0 },
      new ushort[]{ 4160, 3124, 104, 11,  8, 65 },
      new ushort[]{ 4176, 3062,  96, 17,  8,  0, 0, 16, 0, 7, 0x49 },
      new ushort[]{ 4192, 3062,  96, 17, 24,  0, 0, 16, 0, 0, 0x49 },
      new ushort[]{ 4312, 2876,  22, 18,  0,  2 },
      new ushort[]{ 4352, 2874,  62, 18,  0,  0 },
      new ushort[]{ 4476, 2954,  90, 34,  0,  0 },
      new ushort[]{ 4480, 3348,  12, 10, 36, 12, 0, 0, 0, 18, 0x49 },
      new ushort[]{ 4480, 3366,  80, 50,  0,  0 },
      new ushort[]{ 4496, 3366,  80, 50, 12,  0 },
      new ushort[]{ 4768, 3516,  96, 16,  0,  0, 0, 16 },
      new ushort[]{ 4832, 3204,  62, 26,  0,  0 },
      new ushort[]{ 4832, 3228,  62, 51,  0,  0 },
      new ushort[]{ 5108, 3349,  98, 13,  0,  0 },
      new ushort[]{ 5120, 3318, 142, 45, 62,  0 },
     new ushort[] { 5280, 3528,  72, 52,  0,  0 },
      new ushort[]{ 5344, 3516, 142, 51,  0,  0 },
      new ushort[]{ 5344, 3584, 126,100,  0,  2 },
      new ushort[]{ 5360, 3516, 158, 51,  0,  0 },
      new ushort[]{ 5568, 3708,  72, 38,  0,  0 },
      new ushort[]{ 5632, 3710,  96, 17,  0,  0, 0, 16, 0, 0, 0x49 },
      new ushort[]{ 5712, 3774,  62, 20, 10,  2 },
      new ushort[]{ 5792, 3804, 158, 51,  0,  0 },
      new ushort[]{ 5920, 3950, 122, 80,  2,  0 },
      new ushort[]{ 6096, 4056,  72, 34,  0,  0 },
      new ushort[]{ 6288, 4056, 264, 34,  0,  0 },
      new ushort[]{ 8896, 5920, 160, 64,  0,  0 },
    };

    }
}
