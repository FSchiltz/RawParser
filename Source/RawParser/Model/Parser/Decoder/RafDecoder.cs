
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RawNet
{
    class RafDecoder : TiffDecoder
    {
        bool alt_layout;
        uint relativeOffset;

        public RafDecoder(Stream file) : base(file, true)
        {
            alt_layout = false;
            // FUJI has pointers to IFD's at fixed byte offsets
            // So if camera is FUJI, we cannot use ordinary TIFF parser
            //get first 8 char and see if equal fuji
            file.Position = 0;
            var data = new byte[110];
            file.Read(data, 0, 8);
            string dataAsString = System.Text.Encoding.UTF8.GetString(data.Take(8).ToArray());
            if (dataAsString != "FUJIFILM")
            {
                throw new RawDecoderException("Header is wrong");
            }
            //Fuji is indexer reverse endian
            reader = new TIFFBinaryReaderRE(file);
            reader.BaseStream.Position = 8;
            //read next 8 byte
            dataAsString = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8).ToArray()).Trim();
            //4 byte version
            var version = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4).ToArray());
            //8 bytes unknow ??
            var unknow = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8).ToArray()).Trim();
            //32 byte a string (camera model)
            dataAsString = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(32).ToArray()).Trim();
            //Directory
            //4 bytes version
            version = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4).ToArray());
            //20 bytes unkown ??
            dataAsString = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(20).ToArray()).Trim();

            //parse the ifd
            uint first_ifd = reader.ReadUInt32();
            relativeOffset = first_ifd + 12;
            reader.ReadInt32();
            //raw header
            // RAW information IFD on older
            uint third_ifd = reader.ReadUInt32();
            reader.ReadUInt32();
            uint secondIFD = reader.ReadUInt32();
            uint count = reader.ReadUInt32();
            try
            {
                Parse(secondIFD);
            }
            catch (Exception)
            {
                //old format
                ifd = new IFD(Endianness.big, 0);
                //raw image
                var entry = new Tag(TagType.FUJI_STRIPOFFSETS, TiffDataType.LONG, 1);
                entry.data[0] = secondIFD;
                ifd.tags.Add(entry.TagId, entry);
                entry = new Tag(TagType.FUJI_STRIPBYTECOUNTS, TiffDataType.LONG, 1);
                entry.data[0] = count;
                ifd.tags.Add(entry.TagId, entry);
            }
            Parse(first_ifd + 12);
            ParseFuji(third_ifd);
        }

        /* Parse FUJI information */
        /* It is a simpler form of Tiff IFD, so we add them as TiffEntries */
        void ParseFuji(uint offset)
        {
            try
            {
                IFD tempIFD = new IFD(ifd.endian, ifd.Depth);
                TIFFBinaryReaderRE bytes = new TIFFBinaryReaderRE(stream, offset);
                uint entries = bytes.ReadUInt32();

                if (entries > 255)
                    throw new RawDecoderException("ParseFuji: Too many entries");

                for (int i = 0; i < entries; i++)
                {
                    UInt16 tag = bytes.ReadUInt16();
                    uint length = bytes.ReadUInt16();
                    Tag t;

                    // Set types of known tags
                    switch (tag)
                    {
                        case 0x100:
                        case 0x121:
                        case 0x2ff0:
                            t = new Tag((TagType)tag, TiffDataType.SHORT, length / 2);
                            for (int k = 0; k < t.dataCount; k++)
                            {
                                t.data[k] = bytes.ReadUInt16();
                            }
                            break;

                        case 0xc000:
                            // This entry seem to have swapped endianness:
                            t = new Tag((TagType)tag, TiffDataType.LONG, length / 4);
                            for (int k = 0; k < t.dataCount; k++)
                            {
                                t.data[k] = bytes.ReadUInt32();
                            }
                            break;

                        default:
                            t = new Tag((TagType)tag, TiffDataType.UNDEFINED, length);
                            for (int k = 0; k < t.dataCount; k++)
                            {
                                t.data[k] = bytes.ReadByte();
                            }
                            break;
                    }
                    tempIFD.tags.Add(t.TagId, t);
                    //bytes.ReadBytes((int)length);
                }
                ifd.subIFD.Add(tempIFD);
            }
            catch (IOException e)
            {
                throw new RawDecoderException("ParseFuji: IO error occurred during parsing. Skipping the rest");
            }
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.FUJI_STRIPOFFSETS);

            if (data.Count <= 0)
                throw new RawDecoderException("Fuji decoder: Unable to locate raw IFD");

            IFD raw = data[0];
            Int32 height = 0;
            Int32 width = 0;

            var dim = raw.GetEntry(TagType.FUJI_RAWIMAGEFULLHEIGHT);
            if (dim != null)
            {
                height = dim.GetInt(0);
                width = raw.GetEntry(TagType.FUJI_RAWIMAGEFULLWIDTH).GetInt(0);
            }
            else
            {
                Tag wtag = raw.GetEntryRecursive(TagType.IMAGEWIDTH);
                if (wtag != null)
                {
                    if (wtag.dataCount < 2)
                        throw new RawDecoderException("Fuji decoder: Size array too small");
                    height = wtag.GetShort(0);
                    width = wtag.GetShort(1);
                }
            }

            Tag e = raw.GetEntryRecursive(TagType.FUJI_LAYOUT);
            if (e != null)
            {
                if (e.dataCount < 2)
                    throw new RawDecoderException("Fuji decoder: Layout array too small");
                byte[] layout = e.GetByteArray();
                //alt_layout = !(layout[0] >> 7);
            }

            if (width <= 0 || height <= 0)
                throw new RawDecoderException("RAF decoder: Unable to locate image size");

            Tag offsets = raw.GetEntry(TagType.FUJI_STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.FUJI_STRIPBYTECOUNTS);

            if (offsets.dataCount != 1 || counts.dataCount != 1)
                throw new RawDecoderException("RAF Decoder: Multiple Strips found: " + offsets.dataCount + " " + counts.dataCount);

            uint off = offsets.GetUInt(0);
            int count = counts.GetInt(0);

            ushort bps = 16;
            var bpsTag = raw.GetEntryRecursive(TagType.FUJI_BITSPERSAMPLE);
            if (bpsTag != null)
            {
                bps = bpsTag.GetUShort(0);
            }
            else
            {
                rawImage.errors.Add("BPS not found");
            }
            rawImage.ColorDepth = bps;

            // x-trans sensors report 14bpp, but data isn't packed so read as 16bpp
            if (bps == 14) bps = 16;

            // Some fuji SuperCCD cameras include a second raw image next to the first one
            // that is identical but darker to the first. The two combined can produce
            // a higher dynamic range image. Right now we're ignoring it.
            bool double_width = hints.ContainsKey("double_width_unpacked");

            rawImage.raw.dim = new Point2D(width * (double_width ? 2 : 1), height);
            rawImage.Init();
            TIFFBinaryReader input = new TIFFBinaryReader(stream, (uint)(off + raw.RelativeOffset));
            Point2D pos = new Point2D(0, 0);

            if (count * 8 / (width * height) < 10)
            {
                throw new RawDecoderException("Don't know how to decode compressed images");
            }
            else if (double_width)
            {
                Decode16BitRawUnpacked(input, width * 2, height);
            }
            else if (ifd.endian == Endianness.big)
            {
                Decode16BitRawBEunpacked(input, width, height);
            }
            else
            {
                if (hints.ContainsKey("jpeg32_bitorder"))
                    ReadUncompressedRaw(input, rawImage.raw.dim, pos, width * bps / 8, bps, BitOrder.Jpeg32);
                else
                    ReadUncompressedRaw(input, rawImage.raw.dim, pos, width * bps / 8, bps, BitOrder.Plain);
            }
        }

        public override void DecodeMetadata()
        {
            //metadata
            base.DecodeMetadata();
            if (rawImage.metadata.Model == null) throw new RawDecoderException("RAF Meta Decoder: Model name not found");
            if (rawImage.metadata.Make == null) throw new RawDecoderException("RAF Support: Make name not found");
            SetMetadata(rawImage.metadata.Model);
            //get cfa
            var rawifd = ifd.GetIFDsWithTag(TagType.FUJI_LAYOUT);
            if (rawifd != null)
            {
                var cfa = rawifd[0].GetEntry(TagType.FUJI_LAYOUT);
                var t = rawifd[0].GetEntry((TagType)0x0131);
                if (t != null)
                {
                    //fuji cfa
                    rawImage.colorFilter = new ColorFilterArray(new Point2D(6, 6));
                    rawImage.isFujiTrans = true;
                    rawImage.errors.Add("No support for X-trans yet,colour will be wrong");
                    for (int i = 0; i < t.dataCount; i++)
                    {
                        rawImage.colorFilter.cfa[i] = (CFAColor)t.GetInt(i);
                    }
                }
                else if (cfa.GetInt(0) < 4)
                {
                    //bayer cfa
                    rawImage.colorFilter.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0),
                        (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
                }
                else
                {
                    //default to GRBG
                    rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
                }

                //Debug.WriteLine("CFA pattern is not found");
            }
            else
            {
                //rawImage.cfa.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }

            //read lens
            var lens = ifd.GetEntryRecursive((TagType)42036);
            if (lens != null)
                rawImage.metadata.Lens = Common.Trim(lens.DataAsString);
            Tag sep_black = ifd.GetEntryRecursive(TagType.FUJI_RGGBLEVELSBLACK);
            if (sep_black != null)
            {
                if (sep_black.dataCount >= 4)
                {
                    for (int k = 0; k < 4; k++)
                        rawImage.blackLevelSeparate[k] = sep_black.GetInt(k);
                }
            }

            Tag wb = ifd.GetEntryRecursive(TagType.FUJI_WB_GRBLEVELS);
            if (wb != null)
            {
                if (wb.dataCount == 3)
                {
                    rawImage.metadata.WbCoeffs[0] = wb.GetFloat(1) / wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[1] = wb.GetFloat(0) / wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[2] = wb.GetFloat(2) / wb.GetFloat(0);
                }
            }
            else
            {
                wb = ifd.GetEntryRecursive(TagType.FUJIOLDWB);
                if (wb != null)
                {
                    if (wb.dataCount == 8)
                    {
                        rawImage.metadata.WbCoeffs[0] = wb.GetFloat(1) / wb.GetFloat(0);
                        rawImage.metadata.WbCoeffs[1] = wb.GetFloat(0) / wb.GetFloat(0);
                        rawImage.metadata.WbCoeffs[2] = wb.GetFloat(3) / wb.GetFloat(0);
                    }
                }
            }
        }

        protected void SetMetadata(string model)
        {
            if (model.Contains("S2Pro"))
            {
                rawImage.raw.dim.height = 2144;
                rawImage.raw.dim.width = 2880;
                //flip = 6;
            }
            else if (model.Contains("X-Pro1"))
            {
                rawImage.raw.dim.width -= 168;
            }
            else if (model.Contains("FinePix X100"))
            {
                rawImage.raw.dim.width -= 144;
                rawImage.raw.offset.width = 74;
            }
            else
            {
                //maximum = (is_raw == 2 && shot_select) ? 0x2f00 : 0x3e00;
                //top_margin = (raw_height - height) >> 2 << 1;
                //left_margin = (raw_width - width) >> 2 << 1;
                // if (rawImage.raw.dim.width == 2848 || rawImage.raw.dim.width == 3664) filters = 0x16161616;
                //if (rawImage.raw.dim.width == 4032 || rawImage.raw.dim.width == 4952 || rawImage.raw.dim.width == 6032) rawImage.raw.offset.width = 0;
                if (rawImage.raw.dim.width == 3328)
                {
                    rawImage.raw.offset.width = 34;
                    rawImage.raw.dim.width -= 66;
                }
                else if (rawImage.raw.dim.width == 4936)
                    rawImage.raw.offset.width = 4;

                if (model.Contains("HS50EXR") || model.Contains("F900EXR"))
                {
                    rawImage.raw.dim.width += 2;
                    rawImage.raw.offset.width = 0;
                }
            }
        }

        public override Thumbnail DecodeThumb()
        {
            IFD preview = ifd.GetIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT)[0];
            //no thumbnail
            if (preview == null) return null;

            var thumb = preview.GetEntry(TagType.JPEGINTERCHANGEFORMAT);
            var size = preview.GetEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
            if (size == null || thumb == null) return null;


            reader.Position = (uint)(thumb.data[0]) + preview.RelativeOffset;
            Thumbnail temp = new Thumbnail()
            {
                data = reader.ReadBytes(Convert.ToInt32(size.data[0])),
                Type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }
    }
}