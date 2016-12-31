
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RawNet
{
    class RafDecoder : TiffDecoder
    {
        bool alt_layout;

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
            dataAsString = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(8).ToArray());
            //4 byte version
            var version = reader.ReadUInt32();
            //8 bytes unknow ??
            var unknow = reader.ReadUInt64();
            //32 byte a string (camera model)
            dataAsString = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(32).ToArray());
            //Directory
            //4 bytes version
            version = reader.ReadUInt32();
            //20 bytes unkown ??
            dataAsString = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(20).ToArray());

            // First IFD typically JPEG and EXIF
            //position should be 84
            uint first_ifd = reader.ReadUInt32();
            first_ifd += 12;
            if (stream.Length <= first_ifd)
                throw new RawDecoderException("File too small (FUJI first IFD)");
            reader.ReadBytes(6);//?? test position should be 92
            // RAW information IFD on older
            uint third_ifd = (uint)(data[95] | (data[94] << 8) | (data[93] << 16) | (data[92] << 24));
            if (stream.Length <= third_ifd)
                third_ifd = 0;

            // RAW IFD on newer, pointer to raw data on older models, so we try parsing first
            // And adds it as data if parsin fails
            reader.ReadBytes(5);
            //position should be 100
            uint second_ifd = reader.ReadUInt32();
            if (stream.Length <= second_ifd)
                second_ifd = 0;        

            // Open the IFDs and merge them
            ifd = ParseIFD(first_ifd);
            if (second_ifd != 0)
            {
                try
                {
                    ifd.subIFD.Add(ParseIFD(second_ifd));
                }
                catch (Exception e)
                {
                    Tag entry = new Tag(TagType.FUJI_STRIPOFFSETS, TiffDataType.LONG, 1);
                    entry.data[0] = second_ifd;
                    ifd.tags.Add(entry.TagId, entry);
                    entry = new Tag(TagType.FUJI_STRIPBYTECOUNTS, TiffDataType.LONG, 1);
                    uint max_size = (uint)(stream.Length - second_ifd);
                    entry.data[0] = max_size;
                    ifd.tags.Add(entry.TagId, entry);
                }
            }

            if (third_ifd != 0)
            {
                ParseFuji(third_ifd, ifd);
            }
        }

        /* Parse FUJI information */
        /* It is a simpler form of Tiff IFD, so we add them as TiffEntries */
        void ParseFuji(uint offset, IFD target_ifd)
        {
            try
            {
                TIFFBinaryReaderRE bytes = new TIFFBinaryReaderRE(stream, offset);
                uint entries = bytes.ReadUInt32();

                if (entries > 255)
                    throw new RawDecoderException("ParseFuji: Too many entries");
                IFD target = new IFD();
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
                    target_ifd.tags.Add(t.TagId, t);
                    //bytes.ReadBytes((int)length);
                }
            }
            catch (IOException e)
            {
                throw new RawDecoderException("ParseFuji: IO error occurred during parsing. Skipping the rest");
            }
        }

        private IFD ParseIFD(uint offset)
        {
            IFD temp;
            //parse the ifd
            if (stream.Length < 16)
                throw new RawDecoderException("Not a TIFF file (size too small)");
            Endianness endian = Endianness.little;
            byte[] buffer = new byte[5];

            stream.Position = offset;
            stream.Read(buffer, 0, 4);
            if (buffer[0] == 0x4D || buffer[1] == 0x4D)
            {
                //open binaryreader
                reader = new TIFFBinaryReaderRE(stream);
                endian = Endianness.big;
            }
            else if (buffer[0] == 0x49 || buffer[1] == 0x49)
            {
                reader = new TIFFBinaryReader(stream);
            }
            else
            {
                throw new RawDecoderException("Not a TIFF file (ID)");
            }

            if (buffer[2] != 42 && buffer[2] != 0x52 && buffer[2] != 0x55 && buffer[2] != 0x4f)
            {
                // ORF has 0x52, RW2 0x55 - Brillant!
                throw new RawDecoderException("Not a TIFF file (magic 42)");
            }
            reader.Position = offset + 4;
            temp = new IFD(reader, reader.ReadUInt32(), endian, 0);
            uint nextIFD = ifd.NextOffset;
            return temp;
        }
        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.FUJI_STRIPOFFSETS);

            if (data.Count > 0)
                throw new RawDecoderException("Fuji decoder: Unable to locate raw IFD");

            IFD raw = data[0];
            Int32 height = 0;
            Int32 width = 0;

            var dim = raw.GetEntry(TagType.FUJI_RAWIMAGEFULLHEIGHT);
            if (dim != null)
            {
                height = dim.GetInt(0);
                width = dim.GetInt(0);
            }
            else
            {
                Tag wtag = raw.GetEntry(TagType.IMAGEWIDTH);
                if (wtag != null)
                {
                    if (wtag.dataCount < 2)
                        throw new RawDecoderException("Fuji decoder: Size array too small");
                    height = wtag.GetShort(0);
                    width = wtag.GetShort(1);
                }
            }

            Tag e = raw.GetEntry(TagType.FUJI_LAYOUT);
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

            int bps = 16;
            var bpsTag = raw.GetEntry(TagType.FUJI_BITSPERSAMPLE);
            if (bpsTag != null)
            {
                bps = bpsTag.GetInt(0);
            }

            // x-trans sensors report 14bpp, but data isn't packed so read as 16bpp
            if (bps == 14) bps = 16;

            // Some fuji SuperCCD cameras include a second raw image next to the first one
            // that is identical but darker to the first. The two combined can produce
            // a higher dynamic range image. Right now we're ignoring it.
            bool double_width = hints.ContainsKey("double_width_unpacked");

            rawImage.raw.dim = new Point2D(width * (double_width ? 2 : 1), height);
            rawImage.Init();
            TIFFBinaryReader input = new TIFFBinaryReader(stream, off);
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
            base.DecodeMetadata();
            if (rawImage.metadata.Model == null) throw new RawDecoderException("RAF Meta Decoder: Model name not found");
            if (rawImage.metadata.Make == null) throw new RawDecoderException("RAF Support: Make name not found");

            Tag sep_black = ifd.GetEntryRecursive(TagType.FUJI_RGGBLEVELSBLACK);
            if (sep_black != null)
            {
                if (sep_black.dataCount == 4)
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
                    rawImage.metadata.WbCoeffs[0] = wb.GetFloat(1);
                    rawImage.metadata.WbCoeffs[1] = wb.GetFloat(0);
                    rawImage.metadata.WbCoeffs[2] = wb.GetFloat(2);
                }
            }
            else
            {
                wb = ifd.GetEntryRecursive(TagType.FUJIOLDWB);
                if (wb != null)
                {
                    if (wb.dataCount == 8)
                    {
                        rawImage.metadata.WbCoeffs[0] = wb.GetFloat(1);
                        rawImage.metadata.WbCoeffs[1] = wb.GetFloat(0);
                        rawImage.metadata.WbCoeffs[2] = wb.GetFloat(3);
                    }
                }
            }
        }
    }
}
