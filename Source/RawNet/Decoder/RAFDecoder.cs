using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RawNet.Decoder
{
    class RAFDecoder : TIFFDecoder
    {
        //bool alt_layout;
        uint relativeOffset;

        public RAFDecoder(Stream file) : base(file, true)
        {
            //alt_layout = false;
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
            reader = new ImageBinaryReaderBigEndian(file);
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
                ifd = new IFD(Endianness.Big, 0);
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

        public override Thumbnail DecodeThumb()
        {
            //find the preview IFD (usually the first if any)
            List<IFD> potential = ifd.GetIFDsWithTag(TagType.NEWSUBFILETYPE);
            IFD thumbIFD = null;
            if (potential?.Count != 0)
            {
                for (int i = 0; i < potential.Count; i++)
                {
                    var subFile = potential[i].GetEntry(TagType.NEWSUBFILETYPE);
                    if (subFile.GetInt(0) == 1)
                    {
                        thumbIFD = potential[i];
                        break;
                    }
                }
            }

            if (thumbIFD != null)
            {
                //there is a thumbnail
                uint bps = thumbIFD.GetEntry(TagType.BITSPERSAMPLE).GetUInt(0);
                Point2D dim = new Point2D(thumbIFD.GetEntry(TagType.IMAGEWIDTH).GetUInt(0), thumbIFD.GetEntry(TagType.IMAGELENGTH).GetUInt(0));

                int compression = thumbIFD.GetEntry(TagType.COMPRESSION).GetShort(0);
                // Now load the image
                if (compression == 1)
                {
                    // Uncompressed
                    uint cpp = thumbIFD.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                    if (cpp > 4)
                        throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");

                    Tag offsets = thumbIFD.GetEntry(TagType.STRIPOFFSETS);
                    Tag counts = thumbIFD.GetEntry(TagType.STRIPBYTECOUNTS);
                    uint yPerSlice = thumbIFD.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);

                    reader.BaseStream.Position = offsets.GetInt(0) + offsets.parent_offset;

                    return new JPEGThumbnail(reader.ReadBytes(counts.GetInt(0)));
                }
                else if (compression == 6)
                {
                    var offset = thumbIFD.GetEntry((TagType)0x0201);
                    var size = thumbIFD.GetEntry((TagType)0x0202);
                    if (size == null || offset == null) return null;

                    //get the makernote offset
                    List<IFD> exifs = ifd.GetIFDsWithTag((TagType)0x927C);

                    if (exifs == null || exifs.Count == 0) return null;

                    Tag makerNoteOffsetTag = exifs[0].GetEntryRecursive((TagType)0x927C);
                    if (makerNoteOffsetTag == null) return null;
                    reader.Position = offset.GetUInt(0) + 10 + makerNoteOffsetTag.dataOffset;
                    return new JPEGThumbnail(reader.ReadBytes(size.GetInt(0)));
                }
                else return null;
            }
            else
            {
                var previews = ifd.GetIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT);

                //no thumbnail
                if (previews?.Count == 0) return null;
                var preview = previews[0];
                var thumb = preview.GetEntry(TagType.JPEGINTERCHANGEFORMAT);
                var size = preview.GetEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
                if (size == null || thumb == null) return null;

                reader.Position = thumb.GetUInt(0) + thumb.parent_offset;
                return new JPEGThumbnail(reader.ReadBytes(size.GetInt(0)));
            }
        }

        /* Parse FUJI information */
        /* It is a simpler form of Tiff IFD, so we add them as TiffEntries */
        void ParseFuji(uint offset)
        {
            try
            {
                IFD tempIFD = new IFD(ifd.endian, ifd.Depth);
                ImageBinaryReaderBigEndian bytes = new ImageBinaryReaderBigEndian(stream, offset);
                uint entries = bytes.ReadUInt32();

                if (entries > 255)
                    throw new RawDecoderException("Too many entries");

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
            catch (IOException)
            {
                throw new RawDecoderException("IO error occurred during parsing. Skipping the rest");
            }
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.FUJI_STRIPOFFSETS);

            if (data.Count <= 0)
                throw new RawDecoderException("Fuji decoder: Unable to locate raw IFD");

            IFD raw = data[0];
            uint height = 0;
            uint width = 0;

            var dim = raw.GetEntry(TagType.FUJI_RAWIMAGEFULLHEIGHT);
            if (dim != null)
            {
                height = dim.GetUInt(0);
                width = raw.GetEntry(TagType.FUJI_RAWIMAGEFULLWIDTH).GetUInt(0);
            }
            else
            {
                Tag wtag = raw.GetEntryRecursive(TagType.IMAGEWIDTH);
                if (wtag != null)
                {
                    if (wtag.dataCount < 2)
                        throw new RawDecoderException("Fuji decoder: Size array too small");
                    height = wtag.GetUShort(0);
                    width = wtag.GetUShort(1);
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

            int off = offsets.GetInt(0);
            int count = counts.GetInt(0);

            ushort bps = 12;
            var bpsTag = raw.GetEntryRecursive(TagType.FUJI_BITSPERSAMPLE) ?? raw.GetEntryRecursive(TagType.BITSPERSAMPLE);
            if (bpsTag != null)
            {
                bps = bpsTag.GetUShort(0);
            }
            else
            {
                rawImage.errors.Add("BPS not found");
            }
            rawImage.fullSize.ColorDepth = bps;

            // x-trans sensors report 14bpp, but data isn't packed so read as 16bpp
            if (bps == 14) bps = 16;

            // Some fuji SuperCCD cameras include a second raw image next to the first one
            // that is identical but darker to the first. The two combined can produce
            // a higher dynamic range image. Right now we're ignoring it.
            //bool double_width = hints.ContainsKey("double_width_unpacked");

            rawImage.fullSize.dim = new Point2D(width, height);
            rawImage.Init(false);
            ImageBinaryReader input = new ImageBinaryReader(stream, (uint)(off + raw.RelativeOffset));
            Point2D pos = new Point2D(0, 0);

            if (count * 8 / (width * height) < 10)
            {
                throw new RawDecoderException("Don't know how to decode compressed images");
            }
            else if (ifd.endian == Endianness.Big)
            {
                RawDecompressor.Decode16BitRawUnpacked(input, new Point2D(width, height), pos, rawImage);
            }
            else
            {
                //       RawDecompressor.ReadUncompressedRaw(input, rawImage.raw.dim, pos, width * bps / 8, bps, BitOrder.Jpeg32, rawImage);
                RawDecompressor.ReadUncompressedRaw(input, new Point2D(width, height), pos, width * bps / 8, bps, BitOrder.Plain, rawImage);
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
                    rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.Red, CFAColor.Green, CFAColor.Green, CFAColor.Blue);
                }

                //ConsoleContent.Value +=("CFA pattern is not found");
            }
            else
            {
                //rawImage.cfa.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }

            //read lens
            rawImage.metadata.Lens = ifd.GetEntryRecursive((TagType)42036)?.DataAsString;
            rawImage.whitePoint = (1 << rawImage.fullSize.ColorDepth) - 1;
            Tag sep_black = ifd.GetEntryRecursive(TagType.FUJI_RGGBLEVELSBLACK);
            if (sep_black.dataCount > 1) Debug.Assert(sep_black?.GetInt(0) == sep_black?.GetInt(1));
            rawImage.black = sep_black?.GetInt(0) ?? 0;
            /*if (sep_black?.dataCount >= 4)
                {
                    for (int k = 0; k < 4; k++)
                        rawImage.blackLevelSeparate[k] = sep_black.GetInt(k);
                }
            */

            Tag wb = ifd.GetEntryRecursive(TagType.FUJI_WB_GRBLEVELS);

            if (wb?.dataCount == 3)
            {
                rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(1), wb.GetInt(0), wb.GetInt(2), rawImage.fullSize.ColorDepth);
            }
            else
            {
                wb = ifd.GetEntryRecursive(TagType.FUJIOLDWB);

                if (wb?.dataCount == 8)
                {
                    rawImage.metadata.WbCoeffs = new WhiteBalance(wb.GetInt(1), wb.GetInt(0), wb.GetInt(3), rawImage.fullSize.ColorDepth);
                }
            }
        }

        protected void SetMetadata(string model)
        {
            if (model.Contains("S2Pro"))
            {
                rawImage.fullSize.dim.height = 2144;
                rawImage.fullSize.dim.width = 2880;
                //flip = 6;
            }
            else if (model.Contains("X-Pro1"))
            {
                rawImage.fullSize.dim.width -= 168;
            }
            else if (model.Contains("FinePix X100"))
            {
                rawImage.fullSize.dim.width -= 144;
                rawImage.fullSize.offset.width = 74;
            }
            else
            {
                //maximum = (is_raw == 2 && shot_select) ? 0x2f00 : 0x3e00;
                //top_margin = (raw_height - height) >> 2 << 1;
                //left_margin = (raw_width - width) >> 2 << 1;
                // if (rawImage.raw.dim.Width == 2848 || rawImage.raw.dim.Width == 3664) filters = 0x16161616;
                //if (rawImage.raw.dim.Width == 4032 || rawImage.raw.dim.Width == 4952 || rawImage.raw.dim.Width == 6032) rawImage.raw.offset.Width = 0;
                if (rawImage.fullSize.dim.width == 3328)
                {
                    rawImage.fullSize.offset.width = 34;
                    rawImage.fullSize.dim.width -= 66;
                }
                else if (rawImage.fullSize.dim.width == 4936)
                    rawImage.fullSize.offset.width = 4;

                if (model.Contains("HS50EXR") || model.Contains("F900EXR"))
                {
                    rawImage.fullSize.dim.width += 2;
                    rawImage.fullSize.offset.width = 0;
                }
            }
        }

        /*
      private CamRGB[] colorM = { { "Fujifilm E550", 0, 0,
  { 11044,-3888,-1120,-7248,15168,2208,-1531,2277,8069 } },
  { "Fujifilm E900", 0, 0,
  { 9183,-2526,-1078,-7461,15071,2574,-2022,2440,8639 } },
  { "Fujifilm F5", 0, 0,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm F6", 0, 0,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm F77", 0, 0xfe9,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm F7", 0, 0,
  { 10004,-3219,-1201,-7036,15047,2107,-1863,2565,7736 } },
  { "Fujifilm F8", 0, 0,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm S100FS", 514, 0,
  { 11521,-4355,-1065,-6524,13767,3058,-1466,1984,6045 } },
  { "Fujifilm S1", 0, 0,
  { 12297,-4882,-1202,-2106,10691,1623,-88,1312,4790 } },
  { "Fujifilm S20Pro", 0, 0,
  { 10004,-3219,-1201,-7036,15047,2107,-1863,2565,7736 } },
  { "Fujifilm S20", 512, 0x3fff,
  { 11401,-4498,-1312,-5088,12751,2613,-838,1568,5941 } },
  { "Fujifilm S2Pro", 128, 0,
  { 12492,-4690,-1402,-7033,15423,1647,-1507,2111,7697 } },
  { "Fujifilm S3Pro", 0, 0,
  { 11807,-4612,-1294,-8927,16968,1988,-2120,2741,8006 } },
  { "Fujifilm S5Pro", 0, 0,
  { 12300,-5110,-1304,-9117,17143,1998,-1947,2448,8100 } },
  { "Fujifilm S5000", 0, 0,
  { 8754,-2732,-1019,-7204,15069,2276,-1702,2334,6982 } },
  { "Fujifilm S5100", 0, 0,
  { 11940,-4431,-1255,-6766,14428,2542,-993,1165,7421 } },
  { "Fujifilm S5500", 0, 0,
  { 11940,-4431,-1255,-6766,14428,2542,-993,1165,7421 } },
  { "Fujifilm S5200", 0, 0,
  { 9636,-2804,-988,-7442,15040,2589,-1803,2311,8621 } },
  { "Fujifilm S5600", 0, 0,
  { 9636,-2804,-988,-7442,15040,2589,-1803,2311,8621 } },
  { "Fujifilm S6", 0, 0,
  { 12628,-4887,-1401,-6861,14996,1962,-2198,2782,7091 } },
  { "Fujifilm S7000", 0, 0,
  { 10190,-3506,-1312,-7153,15051,2238,-2003,2399,7505 } },
  { "Fujifilm S9000", 0, 0,
  { 10491,-3423,-1145,-7385,15027,2538,-1809,2275,8692 } },
  { "Fujifilm S9500", 0, 0,
  { 10491,-3423,-1145,-7385,15027,2538,-1809,2275,8692 } },
  { "Fujifilm S9100", 0, 0,
  { 12343,-4515,-1285,-7165,14899,2435,-1895,2496,8800 } },
  { "Fujifilm S9600", 0, 0,
  { 12343,-4515,-1285,-7165,14899,2435,-1895,2496,8800 } },
  { "Fujifilm SL1000", 0, 0,
  { 11705,-4262,-1107,-2282,10791,1709,-555,1713,4945 } },
  { "Fujifilm IS-1", 0, 0,
  { 21461,-10807,-1441,-2332,10599,1999,289,875,7703 } },
  { "Fujifilm IS Pro", 0, 0,
  { 12300,-5110,-1304,-9117,17143,1998,-1947,2448,8100 } },
  { "Fujifilm HS10 HS11", 0, 0xf68,
  { 12440,-3954,-1183,-1123,9674,1708,-83,1614,4086 } },
  { "Fujifilm HS2", 0, 0,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm HS3", 0, 0,
  { 13690,-5358,-1474,-3369,11600,1998,-132,1554,4395 } },
  { "Fujifilm HS50EXR", 0, 0,
  { 12085,-4727,-953,-3257,11489,2002,-511,2046,4592 } },
  { "Fujifilm F900EXR", 0, 0,
  { 12085,-4727,-953,-3257,11489,2002,-511,2046,4592 } },
  { "Fujifilm X100S", 0, 0,
  { 10592,-4262,-1008,-3514,11355,2465,-870,2025,6386 } },
  { "Fujifilm X100T", 0, 0,
  { 10592,-4262,-1008,-3514,11355,2465,-870,2025,6386 } },
  { "Fujifilm X100", 0, 0,
  { 12161,-4457,-1069,-5034,12874,2400,-795,1724,6904 } },
  { "Fujifilm X10", 0, 0,
  { 13509,-6199,-1254,-4430,12733,1865,-331,1441,5022 } },
  { "Fujifilm X20", 0, 0,
  { 11768,-4971,-1133,-4904,12927,2183,-480,1723,4605 } },
  { "Fujifilm X30", 0, 0,
  { 12328,-5256,-1144,-4469,12927,1675,-87,1291,4351 } },
  { "Fujifilm X70", 0, 0,
  { 10450,-4329,-878,-3217,11105,2421,-752,1758,6519 } },
  { "Fujifilm X-Pro1", 0, 0,
  { 10413,-3996,-993,-3721,11640,2361,-733,1540,6011 } },
  { "Fujifilm X-Pro2", 0, 0,
  { 11434,-4948,-1210,-3746,12042,1903,-666,1479,5235 } },
  { "Fujifilm X-A1", 0, 0,
  { 11086,-4555,-839,-3512,11310,2517,-815,1341,5940 } },
  { "Fujifilm X-A2", 0, 0,
  { 10763,-4560,-917,-3346,11311,2322,-475,1135,5843 } },
  { "Fujifilm X-E1", 0, 0,
  { 10413,-3996,-993,-3721,11640,2361,-733,1540,6011 } },
  { "Fujifilm X-E2S", 0, 0,
  { 11562,-5118,-961,-3022,11007,2311,-525,1569,6097 } },
  { "Fujifilm X-E2", 0, 0,
  { 8458,-2451,-855,-4597,12447,2407,-1475,2482,6526 } },
  { "Fujifilm X-M1", 0, 0,
  { 10413,-3996,-993,-3721,11640,2361,-733,1540,6011 } },
  { "Fujifilm X-S1", 0, 0,
  { 13509,-6199,-1254,-4430,12733,1865,-331,1441,5022 } },
  { "Fujifilm X-T1", 0, 0,	// also X-T10 
  { 8458,-2451,-855,-4597,12447,2407,-1475,2482,6526 } },
  { "Fujifilm XF1", 0, 0,
  { 13509,-6199,-1254,-4430,12733,1865,-331,1441,5022 } },
  { "Fujifilm XQ", 0, 0,	// XQ1 and XQ2 
  { 9252,-2704,-1064,-5893,14265,1717,-1101,2341,4349 } }};*/
    }
}