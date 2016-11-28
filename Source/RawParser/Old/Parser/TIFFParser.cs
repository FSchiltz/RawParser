using RawParser.Format.IFD;
using RawParser.Reader;
using System;
using System.Collections.Generic;
using System.IO;

namespace RawParser.Parser
{
    class TiffParser : AParser
    {
        protected TIFFBinaryReader fileStream;
        protected IFD ifd, exif;
        protected Header header;
        protected IFD[] subifd;

        public override void Parse(Stream file)
        {
            //Open a binary stream on the file
            fileStream = new TIFFBinaryReader(file);

            //read the first bit to get the endianness of the file           
            if (fileStream.ReadUInt16() == 0x4D4D)
            {
                //File is in reverse bit order
                // fileStream.Dispose(); //DO NOT dispose, because it remove the filestream not the reader and crash the parse
                fileStream = new TIFFBinaryReaderRE(file);
            }

            //read the header
            header = new Header(fileStream, 0);
            //Read the IFD
            ifd = new IFD(fileStream, header.TIFFoffset, true, false);
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            return new Dictionary<ushort, Tag>();
        }

        public override byte[] parsePreview()
        {
            return null;
        }

        public override ushort[] parseRAWImage()
        {
            Tag imageOffsetTag, imageWidthTag, imageHeightTag, imageCompressedTag, photoMetricTag, rowPerStripTag, stripSizeTag;
            if (!ifd.tags.TryGetValue(0x0106, out photoMetricTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0111, out imageOffsetTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0100, out imageWidthTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0101, out imageHeightTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0103, out imageCompressedTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0116, out rowPerStripTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0117, out stripSizeTag)) throw new FormatException("File not correct");

            if ((ushort)photoMetricTag.data[0] == 2)
            {
                Tag samplesPerPixel, bitPerSampleTag;
                if (!ifd.tags.TryGetValue(0x0102, out bitPerSampleTag)) throw new FormatException("File not correct");
                if (!ifd.tags.TryGetValue(0x0115, out samplesPerPixel)) throw new FormatException("File not correct");
                height = Convert.ToUInt32(imageHeightTag.data[0]);
                width = Convert.ToUInt32(imageWidthTag.data[0]);
                //suppose that image are always 8,8,8 or 16,16,16
                colorDepth = (ushort)bitPerSampleTag.data[0];
                ushort[] image = new ushort[width * height * 3];
                long strips = height / (ushort)rowPerStripTag.data[0], lastStrip = height % (ushort)rowPerStripTag.data[0];
                long rowperstrip = Convert.ToInt64(rowPerStripTag.data[0]);
                if ((ushort)imageCompressedTag.data[0] == 1)
                {
                    //not compressed
                    for (int i = 0; i < strips + ((lastStrip == 0) ? 0 : 1); i++)
                    {
                        //for each complete strip
                        //move to the offset
                        fileStream.BaseStream.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y <= lastStrip); y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                //get the pixel
                                //red
                                image[(y + i * rowperstrip) * width * 3 + x * 3] = fileStream.ReadByte();
                                //green
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 1] = fileStream.ReadByte();
                                //blue 
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 2] = fileStream.ReadByte();
                                for (int z = 0; z < (Convert.ToInt32(samplesPerPixel.data[0]) - 3); z++)
                                {
                                    //pass the other pixel if more light
                                    fileStream.ReadByte();
                                }
                            }
                        }
                    }
                }
                else if ((ushort)imageCompressedTag.data[0] == 32773)
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
                        fileStream.BaseStream.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y < lastStrip); y++)
                        {
                            //uncompress line by line of pixel
                            ushort[] temp = new ushort[3 * width];
                            short buffer = 0;
                            int count = 0;
                            for (int x = 0; x < width * 3;)
                            {
                                buffer = fileStream.ReadByte();
                                count = 0;
                                if (buffer >= 0)
                                {
                                    for (int k = 0; k < count; ++k, ++x)
                                    {
                                        temp[x] = fileStream.ReadByte();
                                    }
                                }
                                else
                                {
                                    count = -buffer;
                                    buffer = fileStream.ReadByte();
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
                                    fileStream.ReadByte();
                                }
                            }
                        }
                    }
                }
                else throw new FormatException("Compression mode " + imageCompressedTag.dataAsString + " not supported yet");
                return image;
            }
            else throw new FormatException("Photometric interpretation " + photoMetricTag.dataAsString + " not supported yet");
        }

        /*
         * No thumbnail in tiff
         */
        public override byte[] parseThumbnail()
        {
            return null;
        }
    }
}
