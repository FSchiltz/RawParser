using RawNet.Format.TIFF;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace RawNet.Decoder
{
    internal class TIFFDecoder : RawDecoder
    {
        public IFD ifd = null;

        public TIFFDecoder(Stream stream) : base(stream)
        {
            Parse(0);
        }
        //fuji are special Tiff so we need to first remove uncorrect data before parsing the ifd
        public TIFFDecoder(Stream stream, bool isFuji) : base(stream) { }

        protected void Parse(uint offset)
        {
            //parse the ifd
            if (stream.Length < 16)
                throw new RawDecoderException("Not a TIFF file (size too small)");
            Endianness endian = Endianness.Little;
            byte[] data = new byte[5];
            stream.Position = offset;
            stream.Read(data, 0, 4);
            if (data[0] == 0x4D || data[1] == 0x4D)
            {
                //open binaryreader
                reader = new TIFFBinaryReaderRE(stream);
                endian = Endianness.Big;
                if (data[3] != 42 && data[2] != 0x4f) // ORF sometimes has 0x4f!
                    throw new RawDecoderException("Not a TIFF file (magic 42)");
            }
            else if (data[0] == 0x49 || data[1] == 0x49)
            {
                reader = new TIFFBinaryReader(stream);
                if (data[2] != 42 && data[2] != 0x52 && data[2] != 0x55) // ORF has 0x52, RW2 0x55!
                    throw new RawDecoderException("Not a TIFF file (magic 42)");
            }
            else
            {
                throw new RawDecoderException("Not a TIFF file (ID)");
            }
            reader.Position = offset + 4;

            var newIfd = new IFD(reader, reader.ReadUInt32(), endian, 0, (int)offset);
            if (ifd == null)
            {
                ifd = newIfd;
            }
            else
            {
                ifd.subIFD.Add(newIfd);
            }
            uint nextIFD = newIfd.NextOffset;
            while (nextIFD != 0)
            {
                ifd.subIFD.Add(new IFD(reader, nextIFD, endian, 0, (int)offset));
                if (ifd.subIFD.Count > 100)
                {
                    throw new RawDecoderException("TIFF file has too many SubIFDs, probably broken");
                }
                nextIFD = (ifd.subIFD[ifd.subIFD.Count - 1]).NextOffset;
            }
        }

        public override void DecodeRaw()
        {
            if (!ifd.tags.TryGetValue((TagType)0x0106, out var photoMetricTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0111, out var imageOffsetTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0100, out var imageWidthTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0101, out var imageHeightTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0103, out var imageCompressedTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0116, out var rowPerStripTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue((TagType)0x0117, out var stripSizeTag)) throw new FormatException("File not correct");

            if (photoMetricTag.GetInt(0) == 2)
            {
                if (!ifd.tags.TryGetValue((TagType)0x0102, out var bitPerSampleTag)) throw new FormatException("File not correct");
                if (!ifd.tags.TryGetValue((TagType)0x0115, out var samplesPerPixel)) throw new FormatException("File not correct");
                uint height = imageHeightTag.GetUInt(0);
                uint width = imageWidthTag.GetUInt(0);
                rawImage.isCFA = false;
                rawImage.raw.dim = new Point2D(width, height);
                rawImage.raw.uncroppedDim = rawImage.raw.dim;
                //suppose that image are always 8,8,8 or 16,16,16
                rawImage.ColorDepth = bitPerSampleTag.GetUShort(0);
                rawImage.cpp = 3;
                rawImage.Init();
                rawImage.IsGammaCorrected = false;
                long strips = height / rowPerStripTag.GetLong(0), lastStrip = height % rowPerStripTag.GetLong(0);
                long rowperstrip = rowPerStripTag.GetLong(0);
                uint compression = imageCompressedTag.GetUInt(0);
                if (compression == 1)
                {
                    //not compressed
                    for (int i = 0; i < strips + ((lastStrip == 0) ? 0 : 1); i++)
                    {
                        //for each complete strip
                        //move to the offset
                        reader.Position = imageOffsetTag.GetLong(i);
                        for (int y = 0; y < rowperstrip && !(i == strips && y <= lastStrip); y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                //get the pixel
                                //red
                                rawImage.raw.data[(y + i * rowperstrip) * width * 3 + x * 3] = reader.ReadByte();
                                //green
                                rawImage.raw.data[(y + i * rowperstrip) * width * 3 + x * 3 + 1] = reader.ReadByte();
                                //blue 
                                rawImage.raw.data[(y + i * rowperstrip) * width * 3 + x * 3 + 2] = reader.ReadByte();
                                for (int z = 0; z < (samplesPerPixel.GetInt(0) - 3); z++)
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
                        reader.Position = imageOffsetTag.GetLong(i);
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
                                rawImage.raw.data[(y + i * rowperstrip) * width * 3 + x * 3] = temp[x * 3];
                                //green
                                rawImage.raw.data[(y + i * rowperstrip) * width + x * 3 + 1] = temp[x * 3 + 1];
                                //blue 
                                rawImage.raw.data[(y + i * rowperstrip) * width + x * 3 + 2] = temp[x * 3 + 2];
                                for (int z = 0; z < (samplesPerPixel.GetInt(0) - 3); z++)
                                {
                                    //pass the other pixel if more light
                                    reader.ReadByte();
                                }
                            }
                        }
                    }
                }
                else
                {
                    //we know it's tiff so tiff decoder id
                    var decoder = BitmapDecoder.CreateAsync(BitmapDecoder.TiffDecoderId, stream.AsRandomAccessStream()).AsTask();
                    decoder.Wait();
                    var bitmapasync = decoder.Result.GetSoftwareBitmapAsync().AsTask();
                    bitmapasync.Wait();
                    rawImage.ColorDepth = 8;
                    using (var img = bitmapasync.Result)
                    using (BitmapBuffer buffer = img.LockBuffer(BitmapBufferAccessMode.Write))
                    using (IMemoryBufferReference reference = buffer.CreateReference())
                    {
                        BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                        rawImage.raw.dim = new Point2D((uint)bufferLayout.Width, (uint)bufferLayout.Height);
                        rawImage.Init();
                        unsafe
                        {
                            ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                            for (int y = 0; y < rawImage.raw.dim.height; y++)
                            {
                                long realY = y * rawImage.raw.dim.width * 3;
                                long bufferY = y * rawImage.raw.dim.width * 4 + +bufferLayout.StartIndex;
                                for (int x = 0; x < rawImage.raw.dim.width; x++)
                                {
                                    long realPix = realY + (3 * x);
                                    long bufferPix = bufferY + (4 * x);
                                    rawImage.raw.data[realPix] = temp[bufferPix + 2];
                                    rawImage.raw.data[realPix + 1] = temp[bufferPix + 1];
                                    rawImage.raw.data[realPix + 2] = temp[bufferPix];
                                }
                            }
                        }
                    }
                }
            }
            else throw new FormatException("Photometric interpretation " + photoMetricTag.DataAsString + " not supported yet");
        }

        public override void DecodeMetadata()
        {
            if (rawImage.ColorDepth == 0)
            {
                rawImage.ColorDepth = ifd.GetEntryRecursive(TagType.BITSPERSAMPLE).GetUShort(0);
            }
            var isoTag = ifd.GetEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null) rawImage.metadata.IsoSpeed = isoTag.GetInt(0);
            var fn = ifd.GetEntryRecursive(TagType.APERTUREVALUE);
            if (fn != null)
            {
                rawImage.metadata.Aperture = fn.GetFloat(0);
            }
            else
            {
                fn = ifd.GetEntryRecursive(TagType.FNUMBER);
                if (fn != null) rawImage.metadata.Aperture = fn.GetFloat(0);
            }

            var exposure = ifd.GetEntryRecursive(TagType.EXPOSURETIME);
            if (exposure != null) rawImage.metadata.Exposure = exposure.GetFloat(0);

            if (rawImage.whitePoint == 0)
            {
                Tag whitelevel = ifd.GetEntryRecursive(TagType.WHITELEVEL);
                if (whitelevel != null)
                {
                    rawImage.whitePoint = whitelevel.GetInt(0);
                }
            }

            var time = ifd.GetEntryRecursive(TagType.DATETIMEORIGINAL);
            var timeModify = ifd.GetEntryRecursive(TagType.DATETIMEDIGITIZED);
            if (time != null) rawImage.metadata.TimeTake = time.DataAsString;
            if (timeModify != null) rawImage.metadata.TimeModify = timeModify.DataAsString;
            // Set the make and model
            var t = ifd.GetEntryRecursive(TagType.MAKE);
            var t2 = ifd.GetEntryRecursive(TagType.MODEL);
            if (t != null && t2 != null)
            {
                rawImage.metadata.Make = t.DataAsString.Trim();
                rawImage.metadata.Model = t2.DataAsString.Trim();
            }

            //rotation
            var rotateTag = ifd.GetEntryRecursive(TagType.ORIENTATION);
            if (rotateTag == null)
            {
                rotateTag = ifd.GetEntryRecursive((TagType)0xbc02);
                if (rotateTag != null)
                {
                    switch (rotateTag.GetUShort(0))
                    {
                        case 3:
                        case 2:
                            rawImage.Rotation = 2;
                            break;
                        case 4:
                        case 6:
                            rawImage.Rotation = 1;
                            break;
                        case 7:
                        case 5:
                            rawImage.Rotation = 3;
                            break;
                    }
                }
            }
            else
            {
                switch (rotateTag.GetUShort(0))
                {
                    case 3:
                    case 2:
                        rawImage.Rotation = 2;
                        break;
                    case 6:
                    case 5:
                        rawImage.Rotation = 1;
                        break;
                    case 8:
                    case 7:
                        rawImage.Rotation = 3;
                        break;
                }
            }
            rawImage.metadata.OriginalRotation = rawImage.Rotation;
            rawImage.metadata.RawDim = new Point2D(rawImage.raw.uncroppedDim.width, rawImage.raw.uncroppedDim.height);

            //gps info
            var gps = ifd.GetIFDWithType(IFDType.GPS);
            if (gps != null)
            {
                rawImage.metadata.Gps = new GPSInfo()
                {
                    longitude = gps.GetEntry((TagType)0x02).GetAsDoubleArray(),
                    lattitude = gps.GetEntry((TagType)0x02).GetAsDoubleArray(),
                    longitudeRef = gps.GetEntry((TagType)0x02).DataAsString,
                    lattitudeRef = gps.GetEntry((TagType)0x02).DataAsString,
                    altitude = gps.GetEntry((TagType)0x02).GetFloat(0),
                    altitudeRef = gps.GetEntry((TagType)0x02).GetInt(0)
                };
            }
        }

        public override Thumbnail DecodeThumb()
        {
            //find the preview IFD (usually the first if any)
            try
            {
                List<IFD> potential = ifd.GetIFDsWithTag(TagType.NEWSUBFILETYPE);
                if (potential != null || potential.Count != 0)
                {
                    IFD thumbIFD = null;
                    for (int i = 0; i < potential.Count; i++)
                    {
                        var subFile = potential[i].GetEntry(TagType.NEWSUBFILETYPE);
                        if (subFile.GetInt(0) == 1)
                        {
                            thumbIFD = potential[i];
                            break;
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

                            reader.BaseStream.Position = offsets.GetInt(0);

                            Thumbnail thumb = new Thumbnail()
                            {
                                cpp = cpp,
                                dim = dim,
                                data = reader.ReadBytes(counts.GetInt(0)),
                                Type = ThumbnailType.RAW
                            };
                            return thumb;
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
                            Thumbnail temp = new Thumbnail()
                            {
                                data = reader.ReadBytes(size.GetInt(0)),
                                Type = ThumbnailType.JPEG,
                                dim = new Point2D()
                            };
                            return temp;
                        }
                    }
                }
            }
            catch (Exception)
            {
                //thumbnail are optional so ignore all exception
            }
            return null;
        }
    }
}
