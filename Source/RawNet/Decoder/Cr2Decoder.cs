using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using RawNet.Jpeg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RawNet.Decoder
{
    internal class Cr2Decoder : TIFFDecoder
    {
        int[] sraw_coeffs = new int[3];

        public Cr2Decoder(Stream file) : base(file)
        {
            ScaleValue = true;
        }

        public void DecodeNewFormat()
        {
            /*
            Tag sensorInfoE = ifd.GetEntryRecursive(TagType.CANON_SENSOR_INFO) ?? throw new RawDecoderException("Cr2Decoder: failed to get SensorInfo from MakerNote");
            uint componentsPerPixel = 1;
            IFD raw = ifd.subIFD[3];
            if (raw.GetEntry(TagType.CANON_SRAWTYPE)?.GetUInt(0) == 4)
                componentsPerPixel = 3;

            rawImage = new RawImage(sensorInfoE.GetUInt(1), sensorInfoE.GetUInt(2))
            {
                cpp = componentsPerPixel,
                isCFA = true
            };
            rawImage.raw.ColorDepth = 14;
            rawImage.Init(false);

            List<uint> s_width = new List<uint>();
            Tag cr2SliceEntry = ifd.GetEntryRecursive(TagType.CANONCR2SLICE);
            if (cr2SliceEntry?.GetShort(0) > 0)
            {
                for (int i = 0; i < cr2SliceEntry.GetShort(0); i++)
            s_width.Add(cr2SliceEntry.GetUInt(1));
                s_width.Add(cr2SliceEntry.GetUInt(2));
            }

            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);

            uint offsetY = 0;
            for (int i = 0; i < s_width.Count; i++)
            {
                new LJPEGPlain(reader, rawImage, true, false)
                {
            slicesW = s_width,
            offY = offsetY
                }.StartDecoder(offsets.GetUInt(i), counts.GetUInt(i));
                offsetY += s_width[i];
            }*/

            List<IFD> data = ifd.GetIFDsWithTag((TagType)0xc5d8);
            if (data.Count == 0)
                throw new RawDecoderException("No image data found");

            IFD raw = data[0];
            Tag sensorInfoE = ifd.GetEntryRecursive(TagType.CANON_SENSOR_INFO) ?? throw new RawDecoderException("Failed to get sensor info from Makernote");
            rawImage.fullSize.dim = new Point2D(sensorInfoE.GetUInt(1), sensorInfoE.GetUInt(2));
            //cpp = componentsPerPixel,
            rawImage.isCFA = true;
            rawImage.fullSize.ColorDepth = 14;
            rawImage.Init(false);

            List<RawSlice> slices = new List<RawSlice>();
            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
            // Iterate through all slices
            for (int s = 0; s < offsets.dataCount; s++)
            {
                LJPEGPlain l = new LJPEGPlain(reader, rawImage, false, false);
                RawSlice slice = new RawSlice()
                {
                    offset = offsets.GetUInt(s),
                    count = counts.GetUInt(s)
                };

                SOFInfo sof = l.GetSOF(slice.offset, slice.count);
                slice.w = sof.width * sof.numComponents;
                slice.h = sof.height;

                if (slices.Count != 0 && slices[0].w != slice.w)
                    throw new RawDecoderException("CR2 Decoder: Slice width does not match.");

                if (reader.IsValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            List<uint> s_width = new List<uint>();
            if (raw.tags.ContainsKey(TagType.CANONCR2SLICE))
            {
                Tag ss = raw.GetEntry(TagType.CANONCR2SLICE);
                for (int i = 0; i < ss.GetShort(0); i++)
                {
                    s_width.Add(ss.GetUShort(1));
                }
                s_width.Add(ss.GetUShort(2));
            }
            else
            {
                s_width.Add(slices[0].w);
            }

            uint offY = 0;
            for (int i = 0; i < slices.Count; i++)
            {
                RawSlice slice = slices[i];
                new LJPEGPlain(reader, rawImage, true, false)
                {
                    slicesW = s_width,
                    offY = offY
                }.StartDecoder(slice.offset, slice.count);

                offY += slice.w;
            }

            if (rawImage.metadata.Subsampling.width > 1 || rawImage.metadata.Subsampling.height > 1)
                SRawInterpolate();
        }

        public void DecodeOldFormat()
        {
            uint off = 0;
            var t = ifd.GetEntryRecursive((TagType)0x81);
            if (t != null)
                off = t.GetUInt(0);
            else
            {
                List<IFD> data2 = ifd.GetIFDsWithTag(TagType.CFAPATTERN);
                if (data2.Count == 0)
                    throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                else
                {
                    if (data2[0].tags.ContainsKey(TagType.STRIPOFFSETS))
                        off = data2[0].GetEntry(TagType.STRIPOFFSETS).GetUInt(0);
                    else
                        throw new RawDecoderException("CR2 Decoder: Couldn't find offset");
                }
            }

            var b = new ImageBinaryReader(reader.BaseStream, off + 41);

            uint height = b.ReadUInt16();
            uint width = b.ReadUInt16();

            // Every two lines can be encoded as a single line, probably to try and get
            // better compression by getting the same RGBG sequence in every line

            width *= 2;
            rawImage.fullSize.dim = new Point2D(width, height);


            rawImage.Init(false);
            LJPEGPlain l = new LJPEGPlain(reader, rawImage, false, false);
            try
            {
                l.StartDecoder(off, (uint)(reader.BaseStream.Length - off));
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
            }

            /*
            if (hints.ContainsKey("double_line_ljpeg"))
            {
                // We now have a double width half height image we need to convert to the
                // normal format
                var final_size = new Point2D(width, height);
                RawImage<ushort> procRaw = new RawImage<ushort>();
                procRaw.raw.dim = final_size;
                procRaw.metadata = rawImage.metadata;
                procRaw.Init(false);

                for (int y = 0; y < height; y++)
                {
            for (int x = 0; x < width; x++)
                procRaw.raw.rawView[x] = rawImage.raw.rawView[((y % 2 == 0) ? 0 : width) + x];
                }
                rawImage = procRaw;
            }*/

            var tv = ifd.GetEntryRecursive((TagType)0x123);
            if (tv != null)
            {
                Tag curve = ifd.GetEntryRecursive((TagType)0x123);
                if (curve.dataType == TiffDataType.SHORT && curve.dataCount == 4096)
                {
                    rawImage.table = new TableLookUp(curve.GetUShortArray(), 4096, true);
                    rawImage.ApplyTableLookUp();
                }
            }
        }

        public override void DecodeRaw()
        {
            if (ifd.subIFD.Count < 4)
                DecodeOldFormat();
            else
                DecodeNewFormat();
        }

        public override void DecodeMetadata()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("CR2 Meta Decoder: Model name not found");

            base.DecodeMetadata();

            string mode = "";
            if (rawImage.metadata.Subsampling.height == 2 && rawImage.metadata.Subsampling.width == 2)
                mode = "sRaw1";

            if (rawImage.metadata.Subsampling.height == 1 && rawImage.metadata.Subsampling.width == 2)
                mode = "sRaw2";
            rawImage.metadata.Mode = mode;

            // Fetch the white balance
            try
            {
                Tag wb = ifd.GetEntryRecursive((TagType)0x0a9);
                if (wb != null)
                {
                    //try to use this as white balance
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(0), wb.GetInt(1), wb.GetInt(3), rawImage.fullSize.ColorDepth);
                }
                else
                {
                    wb = ifd.GetEntryRecursive(TagType.CANONCOLORDATA);
                    if (wb != null)
                    {
                        // this entry is a big table, and different cameras store used WB in
                        // different parts, so find the offset, starting with the most common one
                        int offset = 126;

                        offset /= 2;
                        rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(offset), wb.GetInt(offset + 1), wb.GetInt(offset + 3), rawImage.fullSize.ColorDepth);
                    }
                    else
                    {
                        data = null;
                        data = ifd.GetIFDsWithTag(TagType.MODEL);

                        Tag shot_info = ifd.GetEntryRecursive(TagType.CANONSHOTINFO);
                        Tag g9_wb = ifd.GetEntryRecursive(TagType.CANONPOWERSHOTG9WB);
                        if (shot_info != null && g9_wb != null)
                        {
                            UInt16 wb_index = shot_info.GetUShort(7);
                            int wb_offset = (wb_index < 18) ? "012347800000005896"[wb_index] - '0' : 0;
                            wb_offset = wb_offset * 8 + 2;
                            rawImage.metadata.WbCoeffs = new WhiteBalance(g9_wb.GetInt(wb_offset + 1), (g9_wb.GetInt(wb_offset + 0) + g9_wb.GetInt(wb_offset + 3)) / 2, g9_wb.GetInt(wb_offset + 2), rawImage.fullSize.ColorDepth);
                        }
                        else
                        {
                            // WB for the old 1D and 1DS
                            wb = null;
                            wb = ifd.GetEntryRecursive((TagType)0xa4);
                            if (wb != null)
                            {
                                if (wb.dataCount >= 3)
                                {
                                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(0), wb.GetInt(1), wb.GetInt(2), rawImage.fullSize.ColorDepth);
                                }
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

            SetMetadata(rawImage.metadata.Model);
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

            //Set the black area
            if (rawImage.fullSize.offset.Area > 0)
            {
                //there is a black area
                long black = 0;
                for (int y = 0; y < rawImage.fullSize.offset.height; y++)
                {
                    for (int x = 0; x < rawImage.fullSize.offset.width; x++)
                    {
                        black += rawImage.fullSize.rawView[y * rawImage.fullSize.UncroppedDim.width + x];
                    }
                }
                black /= rawImage.fullSize.offset.Area;
                rawImage.black = black;
            }
        }

        protected void SetMetadata(string model)
        {
            //find the color matrice
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
            for (int i = 0; i < canon.GetLength(0); i++)
            {
                if (rawImage.fullSize.dim.width == canon[i][0] && rawImage.fullSize.dim.height == canon[i][1])
                {
                    rawImage.fullSize.offset.width = canon[i][2];
                    rawImage.fullSize.offset.height = canon[i][3];
                    rawImage.fullSize.dim.width -= (canon[i][2]);
                    rawImage.fullSize.dim.height -= (canon[i][3]);
                    rawImage.fullSize.dim.width -= canon[i][4];
                    rawImage.fullSize.dim.height -= canon[i][5];
                    /* mask[0][1] = canon[i][6];
                     mask[0][3] = -canon[i][7];
                     mask[1][1] = canon[i][8];
                     mask[1][3] = -canon[i][9];*/
                    //filters = canon[i][10] * 0x01010101;
                }
                /*if ((unique_id | 0x20000) == 0x2720000)
                {
            rawImage.mOffset.x = 8;
            rawImage.mOffset.y = 16;
                }*/
            }
            if (rawImage.fullSize.ColorDepth == 15)
            {
                switch (rawImage.fullSize.dim.width)
                {
                    case 3344:
                        rawImage.fullSize.dim.width -= 66;
                        break;
                    case 3872:
                        rawImage.fullSize.dim.width -= 72;
                        break;
                }
                if (rawImage.fullSize.dim.height > rawImage.fullSize.dim.width)
                {
                    //SWAP(height, width);
                    //SWAP(raw_height, raw_width);
                }
                if (rawImage.fullSize.dim.width == 7200 && rawImage.fullSize.dim.height == 3888)
                {
                    rawImage.fullSize.dim.width = 6480;
                    rawImage.fullSize.dim.height = 4320;
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
                        rawImage.fullSize.dim.height = 613;
                        rawImage.fullSize.dim.width = 854;
                        //raw_width = 896;
                        //colors = 4;
                        //filters = 0xe1e4e1e4;
                        //load_raw = &CLASS canon_600_load_raw;
                        break;
                    case "PowerShot A5":
                    case "PowerShot A5 Zoom":
                        rawImage.fullSize.dim.height = 773;
                        rawImage.fullSize.dim.width = 960;
                        //raw_width = 992;
                        rawImage.metadata.PixelAspectRatio = 256 / 235.0;
                        //filters = 0x1e4e1e4e;
                        goto canon_a5;
                    case "PowerShot A50":
                        rawImage.fullSize.dim.height = 968;
                        rawImage.fullSize.dim.width = 1290;
                        //rawImage.raw.dim.x = 1320;
                        //filters = 0x1b4e4b1e;
                        goto canon_a5;
                    case "PowerShot Pro70":
                        rawImage.fullSize.dim.height = 1024;
                        rawImage.fullSize.dim.width = 1552;
                        //filters = 0x1e4b4e1b;
                        canon_a5:
                        //colors = 4;
                        rawImage.fullSize.ColorDepth = 10;
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
                        rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.Green, CFAColor.Red, CFAColor.Blue, CFAColor.Green);
                        rawImage.black = (int)rawImage.curve[200];
                        break;
                }
        }

        #region sraw
        uint GetHue()
        {
            var tc = ifd.GetEntryRecursive((TagType)0x10);
            if (tc == null)
            {
                return 0;
            }
            uint model_id = ifd.GetEntryRecursive((TagType)0x10).GetUInt(0);
            if (model_id >= 0x80000281 || model_id == 0x80000218)
                return ((rawImage.metadata.Subsampling.height * rawImage.metadata.Subsampling.width) - 1) >> 1;

            return (rawImage.metadata.Subsampling.height * rawImage.metadata.Subsampling.width);
        }

        // Interpolate and convert sRaw data.
        void SRawInterpolate()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.CANONCOLORDATA);
            if (data.Count == 0)
                throw new RawDecoderException("Unable to locate white balance info.");

            Tag wb = data[0].GetEntry(TagType.CANONCOLORDATA);
            // Offset to sRaw coefficients used to reconstruct uncorrected RGB data.
            Int32 offset = 78;

            sraw_coeffs[0] = wb.GetShort(offset + 0);
            sraw_coeffs[1] = (wb.GetShort(offset + 1) + wb.GetShort(offset + 2) + 1) >> 1;
            sraw_coeffs[2] = wb.GetShort(offset + 3);

            if (rawImage.metadata.Subsampling.height == 1 && rawImage.metadata.Subsampling.width == 2)
            {
                /*
                if (isOldSraw)
            Interpolate_422_old(rawImage.raw.dim.Width / 2, rawImage.raw.dim.Height);
                else if (isNewSraw)
            Interpolate_422_new(rawImage.raw.dim.Width / 2, rawImage.raw.dim.Height);
                else*/
                Interpolate_422(rawImage.fullSize.dim.width / 2, rawImage.fullSize.dim.height);
            }
            else if (rawImage.metadata.Subsampling.height == 2 && rawImage.metadata.Subsampling.width == 2)
            {
                /*
                if (isNewSraw)
            Interpolate_420_new(rawImage.raw.dim.Width / 2, rawImage.raw.dim.Height / 2);
                else*/
                Interpolate_420(rawImage.fullSize.dim.width / 2, rawImage.fullSize.dim.height / 2);
            }
            else
                throw new RawDecoderException("CR2 Decoder: Unknown subsampling");
        }

        private void YUV_TO_RGB1(int Y, int Cb, int Cr, out int r, out int g, out int b)
        {
            r = (ushort)(sraw_coeffs[0] * (Y + ((50 * Cb + 22929 * Cr) >> 12)));
            g = (ushort)(sraw_coeffs[1] * (Y + ((-5640 * Cb - 11751 * Cr) >> 12)));
            b = (ushort)(sraw_coeffs[2] * (Y + ((29040 * Cb - 101 * Cr) >> 12)));
            r >>= 8; g >>= 8; b >>= 8;
        }

        //TODO remove
        private static unsafe void STORE_RGB(ushort* X, int A, int B, int C, int r, int g, int b)
        {
            X[A] = (ushort)Common.Clampbits(r, 16);
            X[B] = (ushort)Common.Clampbits(g, 16);
            X[C] = (ushort)Common.Clampbits(b, 16);
        }

        // sRaw interpolators - ugly as sin, but does the job in reasonably speed
        // Note: Thread safe.
        unsafe void Interpolate_422(uint width, uint height)
        {
            // Last pixel should not be interpolated
            width--;
            int hue = -(int)GetHue() + 16384;
            Parallel.For(0, height, (y) =>
             {
                 fixed (UInt16* c_line = &rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width])
                 {
                     int r, g, b, Y, Cb, Cr, off = 0;
                     for (int x = 0; x < width; x++)
                     {
                         Y = c_line[off];
                         Cb = c_line[off + 1] - hue;
                         Cr = c_line[off + 2] - hue;
                         YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                         STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                         off += 3;

                         Y = c_line[off];
                         int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                         int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                         YUV_TO_RGB1(Y, Cb2, Cr2, out r, out g, out b);
                         STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                         off += 3;
                     }
                     // Last two pixels
                     Y = c_line[off];
                     Cb = c_line[off + 1] - hue;
                     Cr = c_line[off + 2] - hue;
                     YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                     STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                     Y = c_line[off + 3];
                     YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                     STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);
                 }
             });
        }

        // Note: Not thread safe, since it writes inplace.
        unsafe void Interpolate_420(uint width, uint height)
        {
            // Last pixel should not be interpolated
            width--;
            bool atLastLine = false;

            int off, r, g, b, Y, Cb, Cr;
            int hue = -(int)GetHue() + 16384;
            for (int y = 0; y < height; y++)
            {
                fixed (UInt16* c_line = &rawImage.fullSize.rawView[y * 2 * rawImage.fullSize.dim.width], n_line = &rawImage.fullSize.rawView[(y * 2 + 1) * rawImage.fullSize.dim.width], nn_line = &rawImage.fullSize.rawView[(y * 2 + 2) * rawImage.fullSize.dim.width])
                {
                    off = 0;
                    for (int x = 0; x < width; x++)
                    {
                        Y = c_line[off];
                        Cb = c_line[off + 1] - hue;
                        Cr = c_line[off + 2] - hue;
                        YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                        Y = c_line[off + 3];
                        int Cb2 = (Cb + c_line[off + 1 + 6] - hue) >> 1;
                        int Cr2 = (Cr + c_line[off + 2 + 6] - hue) >> 1;
                        YUV_TO_RGB1(Y, Cb2, Cr2, out r, out g, out b);
                        STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                        // Next line
                        Y = n_line[off];
                        int Cb3 = (Cb + nn_line[off + 1] - hue) >> 1;
                        int Cr3 = (Cr + nn_line[off + 2] - hue) >> 1;
                        YUV_TO_RGB1(Y, Cb3, Cr3, out r, out g, out b);
                        STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                        Y = n_line[off + 3];
                        Cb = (Cb + Cb2 + Cb3 + nn_line[off + 1 + 6] - hue) >> 2;  //Left + Above + Right +Below
                        Cr = (Cr + Cr2 + Cr3 + nn_line[off + 2 + 6] - hue) >> 2;
                        YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                        off += 6;
                    }
                    Y = c_line[off];
                    Cb = c_line[off + 1] - hue;
                    Cr = c_line[off + 2] - hue;
                    YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                    Y = c_line[off + 3];
                    YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                    // Next line
                    Y = n_line[off];
                    Cb = (Cb + nn_line[off + 1] - hue) >> 1;
                    Cr = (Cr + nn_line[off + 2] - hue) >> 1;
                    YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                    Y = n_line[off + 3];
                    YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                }
                if (atLastLine)
                {
                    fixed (UInt16* c_line = &(rawImage.fullSize.rawView[(height * 2)]), n_line = &(rawImage.fullSize.rawView[(height * 2 + 1)]))
                    {
                        off = 0;
                        // Last line
                        for (int x = 0; x < width; x++)
                        {
                            Y = c_line[off];
                            Cb = c_line[off + 1] - hue;
                            Cr = c_line[off + 2] - hue;
                            YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                            STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                            Y = c_line[off + 3];
                            YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                            STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                            // Next line
                            Y = n_line[off];
                            YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                            STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                            Y = n_line[off + 3];
                            YUV_TO_RGB1(Y, Cb, Cr, out r, out g, out b);
                            STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                            off += 6;
                        }
                    }
                }
            }
        }

        private void YUV_TO_RGB2(int Y, int Cb, int Cr, out int r, out int g, out int b)
        {
            r = sraw_coeffs[0] * (Y + Cr - 512);
            g = sraw_coeffs[1] * (Y + ((-778 * Cb - (Cr << 11)) >> 12) - 512);
            b = sraw_coeffs[2] * (Y + (Cb - 512));
            r >>= 8; g >>= 8; b >>= 8;
        }

        // Note: Thread safe.
        unsafe void Interpolate_422_old(long width, long height)
        {
            // Last pixel should not be interpolated
            width--;
            int hue = 16384 - (int)GetHue();
            Parallel.For(0, height, (y) =>
            {
                fixed (UInt16* c_line = &(rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width]))
                {
                    int Y, Cb, Cr, r, g, b, off = 0;
                    for (int x = 0; x < width; x++)
                    {
                        Y = c_line[off];
                        Cb = c_line[off + 1] - hue;
                        Cr = c_line[off + 2] - hue;
                        YUV_TO_RGB2(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                        off += 3;

                        Y = c_line[off];
                        int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                        int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                        YUV_TO_RGB2(Y, Cb2, Cr2, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                        off += 3;
                    }
                    // Last two pixels
                    Y = c_line[off];
                    Cb = c_line[off + 1] - 16384;
                    Cr = c_line[off + 2] - 16384;
                    YUV_TO_RGB2(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                    Y = c_line[off + 3];
                    YUV_TO_RGB2(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);
                }
            });
        }

        // Algorithm found in EOS 5d Mk III
        private void YUV_TO_RGB3(int Y, int Cb, int Cr, out int r, out int g, out int b)
        {
            r = sraw_coeffs[0] * (Y + Cr);
            g = sraw_coeffs[1] * (Y + ((-778 * Cb - (Cr << 11)) >> 12));
            b = sraw_coeffs[2] * (Y + Cb);
            r >>= 8; g >>= 8; b >>= 8;
        }

        unsafe void Interpolate_422_new(uint width, uint height)
        {
            // Last pixel should not be interpolated
            width--;

            // Current line
            int hue = -(int)GetHue() + 16384;
            for (int y = 0; y < height; y++)
            {
                fixed (UInt16* c_line = &rawImage.fullSize.rawView[y * rawImage.fullSize.dim.width])
                {
                    int r, g, b, Y, Cb, Cr, off = 0;
                    for (int x = 0; x < width; x++)
                    {
                        Y = c_line[off];
                        Cb = c_line[off + 1] - hue;
                        Cr = c_line[off + 2] - hue;
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                        off += 3;

                        Y = c_line[off];
                        int Cb2 = (Cb + c_line[off + 1 + 3] - hue) >> 1;
                        int Cr2 = (Cr + c_line[off + 2 + 3] - hue) >> 1;
                        YUV_TO_RGB3(Y, Cb2, Cr2, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);
                        off += 3;
                    }
                    // Last two pixels
                    Y = c_line[off];
                    Cb = c_line[off + 1] - 16384;
                    Cr = c_line[off + 2] - 16384;
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                    Y = c_line[off + 3];
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);
                }
            }
        }

        // Note: Not thread safe, since it writes inplace.
        unsafe void Interpolate_420_new(uint width, uint height)
        {
            // Last pixel should not be interpolated
            width--;
            bool atLastLine = false;

            int hue = -(int)GetHue() + 16384;
            int off, r, g, b, Y, Cb, Cr;
            for (int y = 0; y < height; y++)
            {
                fixed (UInt16* c_line = &rawImage.fullSize.rawView[y * 2 * rawImage.fullSize.dim.width], n_line = &rawImage.fullSize.rawView[(y * 2 + 1) * rawImage.fullSize.dim.width], nn_line = &rawImage.fullSize.rawView[(y * 2 + 2) * rawImage.fullSize.dim.width])
                {
                    off = 0;
                    for (int x = 0; x < width; x++)
                    {
                        Y = c_line[off];
                        Cb = c_line[off + 1] - hue;
                        Cr = c_line[off + 2] - hue;
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                        Y = c_line[off + 3];
                        int Cb2 = (Cb + c_line[off + 1 + 6] - hue) >> 1;
                        int Cr2 = (Cr + c_line[off + 2 + 6] - hue) >> 1;
                        YUV_TO_RGB3(Y, Cb2, Cr2, out r, out g, out b);
                        STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                        // Next line
                        Y = n_line[off];
                        int Cb3 = (Cb + nn_line[off + 1] - hue) >> 1;
                        int Cr3 = (Cr + nn_line[off + 2] - hue) >> 1;
                        YUV_TO_RGB3(Y, Cb3, Cr3, out r, out g, out b);
                        STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                        Y = n_line[off + 3];
                        Cb = (Cb + Cb2 + Cb3 + nn_line[off + 1 + 6] - hue) >> 2;  //Left + Above + Right +Below
                        Cr = (Cr + Cr2 + Cr3 + nn_line[off + 2 + 6] - hue) >> 2;
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                        off += 6;
                    }
                    Y = c_line[off];
                    Cb = c_line[off + 1] - hue;
                    Cr = c_line[off + 2] - hue;
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                    Y = c_line[off + 3];
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                    // Next line
                    Y = n_line[off];
                    Cb = (Cb + nn_line[off + 1] - hue) >> 1;
                    Cr = (Cr + nn_line[off + 2] - hue) >> 1;
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                    Y = n_line[off + 3];
                    YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                    STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                }
            }
            if (atLastLine)
            {
                fixed (UInt16* c_line = &(rawImage.fullSize.rawView[(height * 2)]), n_line = &(rawImage.fullSize.rawView[(height * 2 + 1)]))
                {
                    off = 0;

                    // Last line
                    for (int x = 0; x < width; x++)
                    {
                        Y = c_line[off];
                        Cb = c_line[off + 1] - hue;
                        Cr = c_line[off + 2] - hue;
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off, off + 1, off + 2, r, g, b);

                        Y = c_line[off + 3];
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(c_line, off + 3, off + 4, off + 5, r, g, b);

                        // Next line
                        Y = n_line[off];
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(n_line, off, off + 1, off + 2, r, g, b);

                        Y = n_line[off + 3];
                        YUV_TO_RGB3(Y, Cb, Cr, out r, out g, out b);
                        STORE_RGB(n_line, off + 3, off + 4, off + 5, r, g, b);
                        off += 6;
                    }
                }
            }
        }
        #endregion

        private CamRGB[] colorM = {
            new CamRGB( "Canon EOS D2000", 0, 0,new double[,]    { { 24542, -10860, -3401 },{ -1490, 11370, -297 },{ 2858, -605, 3225 } } ),
            new CamRGB(    "Canon EOS D6000", 0, 0,    new double[,]{ { 20482, -7172, -3125 },{ -1033, 10410, -285 },{2542,226,3136 } } ),
            new CamRGB(    "Canon EOS D30", 0, 0,new double[,]    { { 9805, -2689, -1312 },{ -5803, 13064, 3068 },{ -2438, 3075, 8775 } }  ),
            new CamRGB(    "Canon EOS D60", 0, 0xfa0,    new double[,] { { 6188, -1341, -890, -7168, 14489, 2937, -2640, 3228, 8483 } }        ),
            new CamRGB(    "Canon EOS 5DS", 0, 0x3c96,    new double[,] { { 6250, -711, -808 },{ -5153, 12794, 2636 },{ -1249, 2198, 5610 } }        ),
            new CamRGB(    "Canon EOS 5D Mark III", 0, 0x3c80,   new double[,] { { 6722, -635, -963 }, { -4287, 12460, 2028 },{ -908, 2162, 5668 } }),
            new CamRGB(    "Canon EOS 5D Mark II", 0, 0x3cf0,    new double[,] { { 4716, 603, -830 }, { -7798, 15474, 2480 },{ -1496, 1937, 6651 } } ),
            new CamRGB( "Canon EOS 5D", 0, 0xe6c,    new double[,] { { 6347, -479, -972 },{ -8297, 15954, 2480 },{ -1968, 2131, 7649 } }) ,
            new CamRGB( "Canon EOS 6D", 0, 0x3c82,   new double[,] { { 7034, -804, -1014 },{ -4420, 12564, 2058 },{ -851, 1994, 5758 } } ),
            new CamRGB( "Canon EOS 7D Mark II", 0, 0x3510, new double[,] { { 7268, -1082, - 969 },{ -4186, 11839, 2663 },{ -825, 2029, 5839 } } ),
            new CamRGB( "Canon EOS 7D", 0, 0x3510,    new double[,] { { 6844, -996, -856 },{ -3876, 11761, 2396 }, {-593, 1772, 6198 } }    ),
            new CamRGB(     "Canon EOS 10D", 0, 0xfa0, new double[,] { { 8197, -2000, -1118 },{ -6714, 14335, 2592 }, {-2536, 3178, 8266 } }),
            new CamRGB(    "Canon EOS 20Da", 0, 0,   new double[,] { { 14155, -5065, -1382 }, { -6550, 14633, 2039 },{1623,1824,6561 } }),
            new CamRGB( "Canon EOS 20D", 0, 0xfff, new double[,] { { 6599, -537, -891 },{ -8071, 15783, 2424 },{-1983,2234,7462 } }),
            new CamRGB(  "Canon EOS 30D", 0, 0,   new double[,] { { 6257, -303, -1000 },{ -7880, 15621, 2396 }, {-1714,1904,7046 } } ),
            new CamRGB("Canon EOS 40D", 0, 0x3f60,   new double[,] { { 6071, -747, -856 },{ -7653, 15365, 2441 },{-2025,2553,7315 } } ),
            new CamRGB( "Canon EOS 50D", 0, 0x3d93,   new double[,] { { 4920, 616, -593 },{ -6493, 13964, 2784 },{-1774,3178,7005 } }),
            new CamRGB( "Canon EOS 60D", 0, 0x2ff7,    new double[,] { { 6719, -994, -925 },{ -4408, 12426, 2211 },{-887,2129,6051 } }),
            new CamRGB( "Canon EOS 70D", 0, 0x3bc7,   new double[,] { { 7034, -804, -1014 },{ -4420, 12564, 2058 },{-851,1994,5758 } }),
            new CamRGB( "Canon EOS 80D", 0, 0,  new double[,] { { 7457, -671, -937 },{ -4849, 12495, 2643 },{-1213,2354,5492 } } ),
            new CamRGB( "Canon EOS 100D", 0, 0x350f,   new double[,] { { 6602, -841, -939 },{ -4472, 12458, 2247 },{-975,2039,6148 } } ),
            new CamRGB( "Canon EOS 300D", 0, 0xfa0,   new double[,] { { 8197, -2000, -1118 },{ -6714, 14335, 2592 },{ -2536, 3178, 8266 } } ),
            new CamRGB( "Canon EOS 350D", 0, 0xfff,   new double[,] { { 6018, -617, -965 },{ -8645, 15881, 2975 },{-1530,1719,7642 } } ),
            new CamRGB("Canon EOS 400D", 0, 0xe8e,   new double[,] { { 7054, -1501, -990 },{ -8156, 15544, 2812 },{-1278,1414,7796 } }),
            new CamRGB(  "Canon EOS 450D", 0, 0x390d,   new double[,] { { 5784, -262, -821 },{ -7539, 15064, 2672 },{-1982,2681,7427 } } ),
            new CamRGB( "Canon EOS 500D", 0, 0x3479,   new double[,] { { 4763, 712, -646 },{ -6821, 14399, 2640 },{-1921,3276,6561 } } ),
            new CamRGB( "Canon EOS 550D", 0, 0x3dd7,   new double[,] { { 6941, -1164, -857 },{ -3825, 11597, 2534 },{-416,1540,6039 } } ),
            new CamRGB( "Canon EOS 600D", 0, 0x3510,   new double[,] { { 6461, -907, -882 },{ -4300, 12184, 2378 },{-819,1944,5931 } } ),
            new CamRGB( "Canon EOS 650D", 0, 0x354d,   new double[,] { { 6602, -841, -939 },{ -4472, 12458, 2247 },{-975,2039,6148 } } ),
            new CamRGB( "Canon EOS 700D", 0, 0x3c00,   new double[,] { { 6602, -841, -939 },{ -4472, 12458, 2247 },{-975,2039,6148 } } ),
            new CamRGB( "Canon EOS 750D", 0, 0x368e,   new double[,] { { 6362, -823, -847 },{ -4426, 12109, 2616 },{-743,1857,5635 } } ),
            new CamRGB( "Canon EOS 760D", 0, 0x350f,new double[,]    { { 6362, -823, -847 }, { -4426, 12109, 2616 },{-743,1857,5635 } }),
            new CamRGB(  "Canon EOS 1000D", 0, 0xe43,    new double[,]  { { 6771, -1139, -977 },{ -7818, 15123, 2928 },{-1244,1437,7533 } }),
            new CamRGB(  "Canon EOS 1100D", 0, 0x3510,  new double[,]    { { 6444, -904, -893 },{ -4563, 12308, 2535 },{-903,2016,6728 }}),
            new CamRGB(  "Canon EOS 1200D", 0, 0x37c2,  new double[,]    { { 6461, -907, -882 },{ -4300, 12184, 2378 },{-819,1944,5931 } }),
            new CamRGB( "Canon EOS 1300D", 0, 0x3510,   new double[,]   { { 6939, -1016, -866 },{ -4428, 12473, 2177 },{-1175,2178,6162 } }),
            new CamRGB( "Canon EOS M3", 0, 0,    new double[,]  { { 6362, -823, -847 },{ -4426, 12109, 2616 },{-743,1857,5635 } }),
            new CamRGB( "Canon EOS M10", 0, 0,   new double[,]   { { 6400, -480, -888 },{ -5294, 13416, 2047 },{-1296,2203,6137 } }),
            new CamRGB(  "Canon EOS M", 0, 0,   new double[,]   { { 6602, -841, -939 },{ -4472, 12458, 2247 },{-975,2039,6148 } }),
            new CamRGB(  "Canon EOS-1Ds Mark III", 0, 0x3bb0, new double[,]  { { 5859, -211, -930 }, {-8255, 16017, 2353 },{-1732,1887,7448 } }),
            new CamRGB(  "Canon EOS-1Ds Mark II", 0, 0xe80,  new double[,]    { { 6517, -602, -867 },{ -8180, 15926, 2378 },{-1618,1771,7633 } }),
            new CamRGB( "Canon EOS-1D Mark IV", 0, 0x3bb0,  new double[,]   { { 6014, -220, -795 },{ -4109, 12014, 2361 },{-561,1824,5787 } }),
            new CamRGB(  "Canon EOS-1D Mark III", 0, 0x3bb0,  new double[,] { { 6291, -540, -976 }, {-8350, 16145, 2311 },{-1714,1858,7326 } }),
            new CamRGB(  "Canon EOS-1D Mark II N", 0, 0xe80,   new double[,] { { 6240, -466, -822 },{ -8180, 15825, 2500 },{-1801,1938,8042 } }),
            new CamRGB(  "Canon EOS-1D Mark II", 0, 0xe80,   new double[,]  { { 6264, -582, -724 },{ -8312, 15948, 2504 },{-1744,1919,8664 } }),
            new CamRGB(  "Canon EOS-1DS", 0, 0xe20,   new double[,]  { { 4374, 3631, -1743 }, {-7520, 15212, 2472 },{-2892,3632,8161 } }),
            new CamRGB(   "Canon EOS-1D C", 0, 0x3c4e,   new double[,]  { { 6847, -614, -1014 },{ -4669, 12737, 2139 },{-1197,2488,6846 } }),
            new CamRGB(  "Canon EOS-1D X Mark II", 0, 0,   new double[,]  { { 7596, -978, -967 },{ -4808, 12571, 2503 },{-1398,2567,5752 } }),
            new CamRGB("Canon EOS-1D X",0, 0x3c4e, new double[,]{        { 6847, -614, -1014 },       { -4669, 12737, 2139 },        { -1197, 2488, 6846 }    })    ,
             new CamRGB( "Canon EOS-1D", 0, 0xe20,    new double[,]{ { 6806, -179, -1020 },{ -8097, 16415, 1687 },{-3267,4236,7690 }    }),
             new CamRGB( "Canon EOS C500", 853, 0,        new double[,]{ { 17851, -10604, 922 }, {-7425, 16662, 763 },{-3660,3636,22278 }}),
             new CamRGB( "Canon PowerShot A530", 0, 0 ),	// don't want the A5 matrix 
             new CamRGB( "Canon PowerShot A50", 0, 0,   new double[,]{ { -5300, 9846, 1776 },{ 3436, 684, 3939 },{ -5540, 9879, 6200 },{-1404,11175,217 } }),
             new CamRGB( "Canon PowerShot A5", 0, 0,   new double[,]{ { -4801, 9475, 1952 },{ 2926, 1611, 4094 },{ -5259, 10164, 5947 },{-1554,10883,547 } }),
             new CamRGB( "Canon PowerShot G10", 0, 0,  new double[,]{ { 11093, -3906, -1028 },{ -5047, 12492, 2879 },{-1003,1750,5561 } }),
             new CamRGB( "Canon PowerShot G11", 0, 0,  new double[]   { 12177,-4817,-1069,-1612,9864,2049,-98,850,4471 } ),
             new CamRGB( "Canon PowerShot G12", 0, 0,   new double[]  { 13244,-5501,-1248,-1508,9858,1935,-270,1083,4366  }),
             new CamRGB( "Canon PowerShot G15", 0, 0,   new double[]  { 7474,-2301,-567,-4056,11456,2975,-222,716,4181 } ),
            new CamRGB(  "Canon PowerShot G16", 0, 0,  new double[]  { 8020,-2687,-682,-3704,11879,2052,-965,1921,5556 } ),
            new CamRGB(  "Canon PowerShot G1 X", 0, 0, new double[]    { 7378,-1255,-1043,-4088,12251,2048,-876,1946,5805  }),
             new CamRGB( "Canon PowerShot G1", 0, 0,   new double[]  { -4778,9467,2172,4743,-1141,4344,-5146,9908,6077,-1566,11051,557 }),
             new CamRGB( "Canon PowerShot G2", 0, 0,   new double[]  { 9087,-2693,-1049,-6715,14382,2537,-2291,2819,7790 }),
            new CamRGB(  "Canon PowerShot G3 X", 0, 0, new double[]    { 9701,-3857,-921,-3149,11537,1817,-786,1817,5147 }),
            new CamRGB(  "Canon PowerShot G3", 0, 0,   new double[]  { 9212,-2781,-1073,-6573,14189,2605,-2300,2844,7664 }),
            new CamRGB(  "Canon PowerShot G5 X", 0, 0, new double[]    { 9602,-3823,-937,-2984,11495,1675,-407,1415,5049 }),
             new CamRGB( "Canon PowerShot G5", 0, 0,   new double[]  { 9757,-2872,-933,-5972,13861,2301,-1622,2328,7212 } ),
             new CamRGB( "Canon PowerShot G6", 0, 0,    new double[] { 9877,-3775,-871,-7613,14807,3072,-1448,1305,7485 } ),
             new CamRGB( "Canon PowerShot G7 X", 0, 0,  new double[]   { 9602,-3823,-937,-2984,11495,1675,-407,1415,5049 }),
             new CamRGB( "Canon PowerShot G9 X", 0, 0,  new double[]   { 9602,-3823,-937,-2984,11495,1675,-407,1415,5049 }),
            new CamRGB(  "Canon PowerShot G9", 0, 0,    new double[] { 7368,-2141,-598,-5621,13254,2625,-1418,1696,5743 } ),
            new CamRGB(  "Canon PowerShot Pro1", 0, 0,  new double[]   { 10062,-3522,-999,-7643,15117,2730,-765,817,7323 }),
            new CamRGB(  "Canon PowerShot Pro70", 34, 0, new double[]    { -4155,9818,1529,3939,-25,4522,-5521,9870,6610,-2238,10873,1342  }),
            new CamRGB(  "Canon PowerShot Pro90", 0, 0,  new double[]   { -4963,9896,2235,4642,-987,4294,-5162,10011,5859,-1770,11230,577 }),
            new CamRGB(  "Canon PowerShot S30", 0, 0,    new double[] { 10566,-3652,-1129,-6552,14662,2006,-2197,2581,7670 } ),
            new CamRGB(  "Canon PowerShot S40", 0, 0,   new double[]  { 8510,-2487,-940,-6869,14231,2900,-2318,2829,9013}),
            new CamRGB(  "Canon PowerShot S45", 0, 0,   new double[]  { 8163,-2333,-955,-6682,14174,2751,-2077,2597,8041  }),
            new CamRGB(  "Canon PowerShot S50", 0, 0,   new double[]  { 8882,-2571,-863,-6348,14234,2288,-1516,2172,6569  }),
            new CamRGB(  "Canon PowerShot S60", 0, 0,   new double[]  { 8795,-2482,-797,-7804,15403,2573,-1422,1996,7082  }),
            new CamRGB("Canon PowerShot S70", 0, 0,    new double[] { 9976,-3810,-832,-7115,14463,2906,-901,989,7889 } ),
            new CamRGB(  "Canon PowerShot S90", 0, 0,  new double[]   { 12374,-5016,-1049,-1677,9902,2078,-83,852,4683  }),
            new CamRGB(  "Canon PowerShot S95", 0, 0,    new double[] { 13440,-5896,-1279,-1236,9598,1931,-180,1001,4651  }),
            new CamRGB(  "Canon PowerShot S100", 0, 0,   new double[]  { 7968,-2565,-636,-2873,10697,2513,180,667,4211 }),
            new CamRGB(  "Canon PowerShot S110", 0, 0,   new double[]  { 8039,-2643,-654,-3783,11230,2930,-206,690,4194  }),
            new CamRGB(  "Canon PowerShot S120", 0, 0,   new double[]  { 6961,-1685,-695,-4625,12945,1836,-1114,2152,5518  }),
            new CamRGB(  "Canon PowerShot SX1 IS", 0, 0,   new double[]  { 6578,-259,-502,-5974,13030,3309,-308,1058,4970  }),
            new CamRGB(   "Canon PowerShot SX50 HS", 0, 0,   new double[]  { 12432,-4753,-1247,-2110,10691,1629,-412,1623,4926  }),
            new CamRGB(   "Canon PowerShot SX60 HS", 0, 0,   new double[]  { 13161,-5451,-1344,-1989,10654,1531,-47,1271,4955  }),
            new CamRGB(   "Canon PowerShot A3300", 0, 0,     new double[]     { 10826,-3654,-1023,-3215,11310,1906,0,999,4960  }),
            new CamRGB(   "Canon PowerShot A470", 0, 0,      new double[] { 12513,-4407,-1242,-2680,10276,2405,-878,2215,4734  }),
            new CamRGB(  "Canon PowerShot A610", 0, 0,      new double[] { 15591,-6402,-1592,-5365,13198,2168,-1300,1824,5075  }),
            new CamRGB( "Canon PowerShot A620", 0, 0,      new double[] { 15265,-6193,-1558,-4125,12116,2010,-888,1639,5220 } ),
            new CamRGB( "Canon PowerShot A630", 0, 0,      new double[] { 14201,-5308,-1757,-6087,14472,1617,-2191,3105,5348  }),
            new CamRGB( "Canon PowerShot A640", 0, 0,      new double[] { 13124,-5329,-1390,-3602,11658,1944,-1612,2863,4885  }),
            new CamRGB(  "Canon PowerShot A650", 0, 0,      new double[] { 9427,-3036,-959,-2581,10671,1911,-1039,1982,4430  }),
            new CamRGB(  "Canon PowerShot A720", 0, 0,      new double[] { 14573,-5482,-1546,-1266,9799,1468,-1040,1912,3810  }),
            new CamRGB(  "Canon PowerShot S3 IS", 0, 0,     new double[] { 14062,-5199,-1446,-4712,12470,2243,-1286,2028,4836  }),
            new CamRGB(  "Canon PowerShot SX110 IS", 0, 0,  new double[]      { 14134,-5576,-1527,-1991,10719,1273,-1158,1929,3581  }),
            new CamRGB(  "Canon PowerShot SX220", 0, 0,     new double[] { 13898,-5076,-1447,-1405,10109,1297,-244,1860,3687 } ),
            new CamRGB(  "Canon IXUS 160", 0, 0,            new double[,] {{ 11657, -3781, -1136 },{ -3544, 11262, 2283 },{-160,1219,4700 }}) };

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
