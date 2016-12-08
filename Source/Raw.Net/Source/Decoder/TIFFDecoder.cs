using System;

namespace RawNet
{
    internal class TiffDecoder : RawDecoder
    {
        protected IFD ifd;

        public TiffDecoder(IFD rootifd, ref TIFFBinaryReader file, CameraMetaData meta) : base(ref file, meta)
        {
            decoderVersion = 1;
            ifd = rootifd;
            //check if no 
        }

        protected override RawImage decodeRawInternal()
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
                mRaw.dim = new Point2D((int)width, (int)height);
                mRaw.uncroppedDim = mRaw.dim;
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
                        file.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y <= lastStrip); y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                //get the pixel
                                //red
                                image[(y + i * rowperstrip) * width * 3 + x * 3] = file.ReadByte();
                                //green
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 1] = file.ReadByte();
                                //blue 
                                image[(y + i * rowperstrip) * width * 3 + x * 3 + 2] = file.ReadByte();
                                for (int z = 0; z < (Convert.ToInt32(samplesPerPixel.data[0]) - 3); z++)
                                {
                                    //pass the other pixel if more light
                                    file.ReadByte();
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
                        file.Position = Convert.ToInt64(imageOffsetTag.data[i]);
                        for (int y = 0; y < rowperstrip && !(i == strips && y < lastStrip); y++)
                        {
                            //uncompress line by line of pixel
                            ushort[] temp = new ushort[3 * width];
                            short buffer = 0;
                            int count = 0;
                            for (int x = 0; x < width * 3;)
                            {
                                buffer = file.ReadByte();
                                count = 0;
                                if (buffer >= 0)
                                {
                                    for (int k = 0; k < count; ++k, ++x)
                                    {
                                        temp[x] = file.ReadByte();
                                    }
                                }
                                else
                                {
                                    count = -buffer;
                                    buffer = file.ReadByte();
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
                                    file.ReadByte();
                                }
                            }
                        }
                    }
                }
                else throw new FormatException("Compression mode " + imageCompressedTag.dataAsString + " not supported yet");
                mRaw.cpp = 3;
                mRaw.ColorDepth = colorDepth;
                mRaw.bpp = colorDepth;
                mRaw.rawData = image;
                return mRaw;
            }
            else throw new FormatException("Photometric interpretation " + photoMetricTag.dataAsString + " not supported yet");
        }

        protected override void decodeMetaDataInternal()
        {
            var t = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (t != null) mRaw.metadata.isoSpeed = t.getInt();

            // Set the make and model
            t = ifd.getEntryRecursive(TagType.MAKE);
            var t2 = ifd.getEntryRecursive(TagType.MODEL);
            if (t != null && t != null)
            {
                string make = t.dataAsString;
                string model = t2.dataAsString;
                make = make.Trim();
                model = model.Trim();
                mRaw.metadata.make = make;
                mRaw.metadata.model = model;
                mRaw.metadata.canonical_make = make;
                mRaw.metadata.canonical_model = mRaw.metadata.canonical_alias = model;
                t = ifd.getEntryRecursive(TagType.UNIQUECAMERAMODEL);
                if (t != null)
                {
                    mRaw.metadata.canonical_id = t.dataAsString;
                }
                else
                {
                    mRaw.metadata.canonical_id = make + " " + model;
                }
            }
        }

        protected override void checkSupportInternal()
        {
            //TODO add more check
        }
    }
}
