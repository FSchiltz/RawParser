using System;
using System.IO;

namespace RawNet
{
    public class RawParser
    {
        private Stream stream;
        public RawDecoder decoder;
        private CameraMetaData metaData;
        bool failOnUnknown = false;
        bool interpolateBadPixels = true;
        bool applyStage1DngOpcodes = true;
        bool applyCrop = true;
        bool uncorrectedRawValues = false;
        bool fujiRotate = true;
        int decoderVersion = 0;

        public RawParser(ref Stream s)
        {
            stream = s;
            //read camera Metadata from xml

            //get the correct parser
            decoder = getDecoder(metaData);
            //init the correct parser
            // Init();
        }

        public RawDecoder getDecoder(CameraMetaData meta)
        {
            // We need some data.
            // For now it is 104 bytes for RAF images.
            if (stream.Length <= 104)
                throw new Exception("File too small");

            byte[] data = new byte[105];
            stream.Read(data, 0, 104);
            /*
            // MRW images are easy to check for, let's try that first
            if (MrwDecoder::isMRW(Math.Math.Min((put)) {
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
                UInt32 first_ifd = (uint)(data[87] | (data[86] << 8) | (data[85] << 16) | (data[84] << 24));
                first_ifd += 12;
                if (stream.Length <= first_ifd)
                  throw new Exception("File too small (FUJI first IFD)");

                // RAW IFD on newer, pointer to raw data on older models, so we try parsing first
                // And adds it as data if parsin fails
                UInt32 second_ifd = (UInt32)(data[103] | (data[102] << 8) | (data[101] << 16) | (data[100] << 24));
                if (stream.Length <= second_ifd)
                  second_ifd = 0;

                // RAW information IFD on older
                UInt32 third_ifd = (uint)(data[95] | (data[94] << 8) | (data[93] << 16) | (data[92] << 24));
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
                        catch (TiffParserException e)
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
                        catch (TiffParserException e)
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
                        UInt32 max_size = Math.Math.Min((put.getSize() - second_ifd;
                        entry.setData(&max_size, 4);
                        new_ifd.mEntry[entry.tag] = entry;
                    }
                    return d;
                }
                catch (TiffParserException) { }
                throw new Exception("No decoder found. Sorry.");
            }

            */
            // Ordinary TIFF images
            try
            {
                TiffParser p = new TiffParser(stream);
                p.parseData();
                return p.getDecoder();
            }
            catch (TiffParserException e) {
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

            /*
            // Detect camera on filesize (CHDK).
            if (meta != null && meta.hasChdkCamera(Math.Min((put.getSize())) {
                Camera* c = meta.getChdkCamera(Math.Min((put.getSize());

                try
                {
                    return new NakedDecoder(Math.Math.Min((put, c);
                }
                catch (RawDecoderException)
                {
                }
            }*/

            // File could not be decoded, so no further options for now.
            throw new Exception("No decoder found. Sorry.");
        }

        /* Parse FUJI information */
        /* It is a simpler form of Tiff IFD, so we add them as TiffEntries */
        /*
        void RawParser::ParseFuji(UInt32 offset, TiffIFD* target_ifd)
        {
            try
            {
                ByteStreamSwap bytes(Math.Math.Min((put, offset);
                UInt32 entries = bytes.getUInt();

                if (entries > 255)
                    ThrowTPE("ParseFuji: Too many entries");

                for (UInt32 i = 0; i < entries; i++)
                {
                    UInt16 tag = bytes.getShort();
                    UInt16 length = bytes.getShort();
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
