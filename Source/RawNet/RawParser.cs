using PhotoNet.Common;
using RawNet.Decoder;
using System;
using System.IO;
using Windows.Storage;

namespace RawNet
{
    public static class RawParser
    {
        //private Stream stream;
        //public RawDecoder decoder;
        /*
        bool failOnUnknown = false;
        bool interpolateBadPixels = true;
        bool applyStage1DngOpcodes = true;
        bool applyCrop = true;
        bool uncorrectedRawValues = false;
        bool fujiRotate = true;
        int decoderVersion = 0;*/
        /*
        public RawParser( Stream s, CameraMetaData metaData, string extension)
        {
            stream = s;
            //read camera Metadata from xml

            //get the correct parser
            decoder = GetDecoder(metaData);
            //init the correct parser
            // Init();
        }*/


        public static RawDecoder GetDecoder(Stream stream)
        {
            // We need some data.
            // For now it is 104 bytes for RAF images.
            if (stream.Length <= 104)
                throw new RawDecoderException("File too small");

            byte[] data = new byte[105];
            stream.Read(data, 0, 104);
            /*
            // MRW images are easy to check for, let's try that first
            if (MrwDecoder::isMRW(mInput)) {
                try
                {
                    return new MrwDecoder(Math.Math.Min((put);
                }
                catch (RawDecoderException)
                {
                }
            }*/

            /*
            if (0 == memcmp(&data[0], "ARRI\x12\x34\x56\x78", 8))
            {
                try
                {
                    return new AriDecoder(Math.Math.Min((put);
                }
                catch (RawDecoderException)
                {
                }
            }*/

            // FUJI has pointers to IFD's at fixed byte offsets
            // So if camera is FUJI, we cannot use ordinary TIFF parser
            //get first 8 char and see if equal fuji
            /*
            string dataAsString = System.Text.Encoding.UTF8.GetString(data.Take(8).ToArray());
            if (dataAsString == "FUJIFILM")
            {
                // First IFD typically JPEG and EXIF
                uint first_ifd = (uint)(data[87] | (data[86] << 8) | (data[85] << 16) | (data[84] << 24));
                first_ifd += 12;
                if (stream.Length <= first_ifd)
                  throw new RawDecoderException("File too small (FUJI first IFD)");

                // RAW IFD on newer, pointer to raw data on older models, so we try parsing first
                // And adds it as data if parsin fails
                uint second_ifd = (uint)(data[103] | (data[102] << 8) | (data[101] << 16) | (data[100] << 24));
                if (stream.Length <= second_ifd)
                  second_ifd = 0;

                // RAW information IFD on older
                uint third_ifd = (uint)(data[95] | (data[94] << 8) | (data[93] << 16) | (data[92] << 24));
                if (stream.Length <= third_ifd)
                  third_ifd = 0;

                // Open the IFDs and merge them
                try
                {
                    FileMap* m1 = new FileMap(Math.Math.Min((put, first_ifd);
                    FileMap* m2 = null;
                    TiffParser p(m1);
                    p.parseData();
                    if (second_ifd)
                    {
                        m2 = new FileMap(Math.Math.Min((put, second_ifd);
                        try
                        {
                            TiffParser p2(m2);
                            p2.parseData();
                            p.MergeIFD(&p2);
                        }
                        catch (RawDecoderException e)
                        {
                            delete m2;
                            m2 = null;
                        }
                    }

                    TiffIFD* new_ifd = new TiffIFD(Math.Math.Min((put);
                    p.RootIFD().mSubIFD.push_back(new_ifd);

                    if (third_ifd)
                    {
                        try
                        {
                            ParseFuji(third_ifd, new_ifd);
                        }
                        catch (RawDecoderException e)
                        {
                        }
                    }
                    // Make sure these aren't leaked.
                    RawDecoder* d = p.getDecoder();
                    d.ownedObjects.push_back(m1);
                    if (m2)
                        d.ownedObjects.push_back(m2);

                    if (!m2 && second_ifd)
                    {
                        TiffEntry* entry = new TiffEntry(FUJI_STRIPOFFSETS, TIFF_LONG, 1);
                        entry.setData(&second_ifd, 4);
                        new_ifd.mEntry[entry.tag] = entry;
                        entry = new TiffEntry(FUJI_STRIPBYTECOUNTS, TIFF_LONG, 1);
                        uint max_size = Math.Math.Min((put.getSize() - second_ifd;
                        entry.setData(&max_size, 4);
                        new_ifd.mEntry[entry.tag] = entry;
                    }
                    return d;
                }
                catch (RawDecoderException) { }
                throw new RawDecoderException("No decoder found. Sorry.");
            }


            // Ordinary TIFF images
            try
            {
                TiffParser p = new TiffParser( stream, meta);
                p.parseData();
                return p.getDecoder();
            }
            catch (RawDecoderException)
            {
            }

            /*
            try
            {
                X3fParser parser(mInput);
                return parser.getDecoder();
            }
            catch (RawDecoderException)
            {
            }*/

            /*
            // CIFF images
            try
            {
                CiffParser p(Math.Math.Min((put);
                p.parseData();
                return p.getDecoder();
            }
            catch (CiffParserException)
            {
            }
            */


            // Detect camera on filesize (CHDK).
            /*if (meta != null && meta.hasChdkCamera((uint)stream.Length))
            {
                Camera c = meta.getChdkCamera((uint)stream.Length);
                try
                {
                    return new NakedDecoder( stream, c, meta);
                }
                catch (RawDecoderException)
                {
                }
            }*/

            //try jpeg file
            try
            {
                return new JPGDecoder(stream);
            }
            catch (RawDecoderException)
            {
            }
            // File could not be decoded, so no further options for now.
            throw new FormatException("No decoder found. Sorry.");
        }

        public static RawDecoder GetDecoder(Stream stream, StorageFile file)
        {
            switch (file.FileType.ToUpper())
            {
                //TIFF based raw
                case ".NEF":
                    return new NefDecoder(stream);
                case ".CR2":
                    return new Cr2Decoder(stream);
                case ".ARW":
                    return new ArwDecoder(stream);
                case ".PEF":
                    return new PEFDecoder(stream);
                case ".DNG":
                    return new DNGDecoder(stream);
                case ".ORF":
                    return new ORFDecoder(stream);
                case ".RAW":
                case ".RW2":
                    return new RW2Decoder(stream);
                case ".RAF":
                    return new RAFDecoder(stream);

                //other raw format
                case ".TIFF":
                case ".TIF":
                    return new TIFFDecoder(stream);

                case ".JPG":
                case ".JPEG":
                case ".PNG":
                case ".JXR":
                case ".ICO":
                case ".BMP":
                case ".GIF":
                    return new JPGDecoder(stream);
                default:
                    /*
                    // Detect camera on filesize (CHDK).
                    if (metadata != null && metadata.hasChdkCamera((uint)stream.Length))
                    {
                        Camera c = metadata.getChdkCamera((uint)stream.Length);
                        try
                        {
                            return new NakedDecoder( stream, c, metadata);
                        }
                        catch (RawDecoderException)
                        {
                        }
                    }*/
                    throw new RawDecoderException("No decoder found sorry");
            }
        }

        /* Parse FUJI information */
        /* It is a simpler form of Tiff IFD, so we add them as TiffEntries */
        /*
        void RawParser::ParseFuji(uint offset, TiffIFD* target_ifd)
        {
            try
            {
                ByteStreamSwap bytes(Math.Math.Min((put, offset);
                uint entries = bytes.GetUInt(0);

                if (entries > 255)
                    ThrowTPE("ParseFuji: Too many entries");

                for (int i = 0; i < entries; i++)
                {
                    UInt16 tag = bytes.GetShort(0);
                    UInt16 length = bytes.GetShort(0);
                    TiffEntry* t;

                    // Set types of known tags
                    switch (tag)
                    {
                        case 0x100:
                        case 0x121:
                        case 0x2ff0:
                            t = new TiffEntryBE((TiffTag)tag, TIFF_SHORT, length / 2, bytes.getData());
                            break;

                        case 0xc000:
                            // This entry seem to have swapped endianness:
                            t = new TiffEntry((TiffTag)tag, TIFF_LONG, length / 4, bytes.getData());
                            break;

                        default:
                            t = new TiffEntry((TiffTag)tag, TIFF_UNDEFINED, length, bytes.getData());
                    }

                    target_ifd.mEntry[t.tag] = t;
                    bytes.skipBytes(length);
                }
            }
            catch (IOException e)
            {
                ThrowTPE("ParseFuji: IO error occurred during parsing. Skipping the rest");
            }

        }
        */
    }
}
