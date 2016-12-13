using System;
using System.IO;

namespace RawNet
{
    internal class TiffDecoder : RawDecoder
    {
        protected IFD ifd;

        public TiffDecoder(ref Stream stream, CameraMetaData meta) : base(meta)
        {
            decoderVersion = 1;
            //parse the ifd
            if (stream.Length < 16)
                throw new TiffParserException("Not a TIFF file (size too small)");
            Endianness endian = Endianness.little;
            byte[] data = new byte[5];
            stream.Position = 0;
            stream.Read(data, 0, 4);
            if (data[0] == 0x4D || data[1] == 0x4D)
            {
                //open binaryreader
                reader = new TIFFBinaryReaderRE(stream);
                endian = Endianness.big;

                if (data[3] != 42 && data[2] != 0x4f) // ORF sometimes has 0x4f, Lovely!
                    throw new TiffParserException("Not a TIFF file (magic 42)");
            }
            else if (data[0] == 0x49 || data[1] == 0x49)
            {
                reader = new TIFFBinaryReader(stream);
                if (data[2] != 42 && data[2] != 0x52 && data[2] != 0x55) // ORF has 0x52, RW2 0x55 - Brillant!
                    throw new TiffParserException("Not a TIFF file (magic 42)");
            }
            else
            {
                throw new TiffParserException("Not a TIFF file (ID)");
            }

            UInt32 nextIFD;
            reader.Position = 4;
            nextIFD = reader.ReadUInt32();
            ifd = new IFD(reader, nextIFD, endian, 0);
            nextIFD = ifd.nextOffset;

            while (nextIFD != 0)
            {
                ifd.subIFD.Add(new IFD(reader, nextIFD, endian, 0));
                if (ifd.subIFD.Count > 100)
                {
                    throw new TiffParserException("TIFF file has too many SubIFDs, probably broken");
                }
                nextIFD = (ifd.subIFD[ifd.subIFD.Count - 1]).nextOffset;
            }
            
            //check if no 
        }

        protected override void decodeRawInternal()
        {
            if (!ifd.tags.TryGetValue((TagType)0x0106, out var photoMetricTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0111, out var imageOffsetTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0100, out var imageWidthTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0101, out var imageHeightTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0103, out var imageCompressedTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0116, out var rowPerStripTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0117, out var stripSizeTag)) throw new FormatException("File not correct");

            if ((ushort)photoMetricTag.data[0] == 2)
            {
                if (!ifd.tags.TryGetValue((TagType)0x0102, out var bitPerSampleTag)) throw new FormatException("File not correct");
                if (!ifd.tags.TryGetValue((TagType)0x0115, out var samplesPerPixel)) throw new FormatException("File not correct");
                uint height = Convert.ToUInt32(imageHeightTag.data[0]);
                uint width = Convert.ToUInt32(imageWidthTag.data[0]);
                rawImage.dim = new Point2D((int)width, (int)height);
                rawImage.uncroppedDim = rawImage.dim;
                //suppose that image are always 8,8,8 or 16,16,16
                ushort colorDepth = (ushort)bitPerSampleTag.data[0];
                ushort[] image = new ushort[width * height * 3];
                long strips = height / Convert.ToInt64(rowPerStripTag.data[0]), lastStrip = height % Convert.ToInt64(rowPerStripTag.data[0]);
                long rowperstrip = Convert.ToInt64(rowPerStripTag.data[0]);
                uint compression = imageCompressedTag.getUInt();
                if (compression == 1)
                {
                    //not compressed
                    for (int i = 0; i < strips + ((lastStrip == 0) ? 0 : 1); i++)
                    {
                        //for each complete strip
                        //move to the offset
                        reader.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y <= lastStrip); y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                //get the pixel
                                //red
                                image[(y + i * rowperstrip) * width * 3 + x * 3] = reader.ReadByte();
                                //green
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 1] = reader.ReadByte();
                                //blue 
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 2] = reader.ReadByte();
                                for (int z = 0; z < (Convert.ToInt32(samplesPerPixel.data[0]) - 3); z++)
                                {
                                    //pass the other pixel if more light
                                    reader.ReadByte();
                                }
                            }
                        }
                    }
                }
                else if (compression == 32773)
                {
                    //compressed
                    /*Loop until you get the number of unpacked bytes you are expecting:
                    Read the next source byte into n.
                    If n is between 0 and 127 inclusive, copy the next n+1 bytes literally.
                    Else if n is between - 127 and - 1 inclusive, copy the next byte -n + 1
                    times.
                    Else if n is - 128, noop.
                    Endloop
                    */
                    //not compressed
                    for (int i = 0; i < strips + ((lastStrip == 0) ? 0 : 1); i++)
                    {
                        //for each complete strip
                        //move to the offset
                        reader.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y < lastStrip); y++)
                        {
                            //uncompress line by line of pixel
                            ushort[] temp = new ushort[3 * width];
                            short buffer = 0;
                            int count = 0;
                            for (int x = 0; x < width * 3;)
                            {
                                buffer = reader.ReadByte();
                                count = 0;
                                if (buffer >= 0)
                                {
                                    for (int k = 0; k < count; ++k, ++x)
                                    {
                                        temp[x] = reader.ReadByte();
                                    }
                                }
                                else
                                {
                                    count = -buffer;
                                    buffer = reader.ReadByte();
                                    for (int k = 0; k < count; ++k, ++x)
                                    {
                                        temp[x] = (ushort)buffer;
                                    }
                                }
                            }

                            for (int x = 0; x < width * 3; x++)
                            {

                                //red
                                image[(y + i * rowperstrip) * width * 3 + x * 3] = temp[x * 3];
                                //green
                                image[(y + i * rowperstrip) * width + x * 3 + 1] = temp[x * 3 + 1];
                                //blue 
                                image[(y + i * rowperstrip) * width + x * 3 + 2] = temp[x * 3 + 2];
                                for (int z = 0; z < ((int)samplesPerPixel.data[0] - 3); z++)
                                {
                                    //pass the other pixel if more light
                                    reader.ReadByte();
                                }
                            }
                        }
                    }
                }
                else throw new FormatException("Compression mode " + imageCompressedTag.DataAsString + " not supported yet");
                rawImage.cpp = 3;
                rawImage.ColorDepth = colorDepth;
                rawImage.bpp = colorDepth;
                rawImage.rawData = image;
            }
            else throw new FormatException("Photometric interpretation " + photoMetricTag.DataAsString + " not supported yet");
        }

        protected override void decodeMetaDataInternal()
        {
            var t = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (t != null) rawImage.metadata.isoSpeed = t.getInt();

            // Set the make and model
            t = ifd.getEntryRecursive(TagType.MAKE);
            var t2 = ifd.getEntryRecursive(TagType.MODEL);
            if (t != null && t != null)
            {
                string make = t.DataAsString;
                string model = t2.DataAsString;
                make = make.Trim();
                model = model.Trim();
                rawImage.metadata.make = make;
                rawImage.metadata.model = model;
                /*
                rawImage.metadata.canonical_make = make;
                rawImage.metadata.canonical_model = rawImage.metadata.canonical_alias = model;
                t = ifd.getEntryRecursive(TagType.UNIQUECAMERAMODEL);
                if (t != null)
                {
                    rawImage.metadata.canonical_id = t.DataAsString;
                }
                else
                {
                    rawImage.metadata.canonical_id = make + " " + model;
                }*/
            }
        }

        protected override void checkSupportInternal()
        {
            //TODO add more check
        }
    }
}
