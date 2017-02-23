using RawNet.Decoder.Decompressor;
using RawNet.Dng;
using RawNet.Format.Tiff;
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
                reader = new TiffBinaryReaderBigEndian(stream);
                endian = Endianness.Big;
                if (data[3] != 42 && data[3] != 0x4f) // ORF sometimes has 0x4f!
                    throw new RawDecoderException("Not a TIFF file (magic 42)");
            }
            else if (data[0] == 0x49 || data[1] == 0x49)
            {
                reader = new TiffBinaryReader(stream);
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
                    throw new RawDecoderException("TIFF file has too many Sub IFDs, probably broken");
                }
                nextIFD = (ifd.subIFD[ifd.subIFD.Count - 1]).NextOffset;
            }
        }

        public override void DecodeRaw()
        {
            int photoMetric = ifd.GetEntryRecursive((TagType)0x0106)?.GetInt(0) ?? throw new FormatException("File not correct"); ;
            if (photoMetric != 2) { throw new FormatException("Photometric interpretation " + photoMetric + " not supported yet"); }

            uint height = ifd.GetEntryRecursive((TagType)0x0101)?.GetUInt(0) ?? throw new FormatException("File not correct");
            uint width = ifd.GetEntryRecursive((TagType)0x0100)?.GetUInt(0) ?? throw new FormatException("File not correct");
            rawImage.isCFA = false;
            rawImage.raw.dim = new Point2D(width, height);
            rawImage.raw.UncroppedDim = rawImage.raw.dim;
            rawImage.raw.ColorDepth = ifd.GetEntryRecursive((TagType)0x0102)?.GetUShort(0) ?? throw new FormatException("File not correct");
            rawImage.raw.cpp = 3;
            rawImage.Init(true);
            rawImage.IsGammaCorrected = false;

            int compression = ifd.GetEntryRecursive((TagType)0x0103)?.GetInt(0) ?? throw new FormatException("File not correct");
            if (compression == 1)
            {
                DecodeUncompressed(ifd, BitOrder.Plain);
            }
            else if (compression == 32773 && rawImage.raw.ColorDepth <= 8)
            {
                /*Loop until you get the number of unpacked bytes you are expecting:
                Read the next source byte into n.
                If n is between 0 and 127 inclusive, copy the next n+1 bytes literally.
                Else if n is between - 127 and - 1 inclusive, copy the next byte -n + 1
                times.
                Else if n is - 128, noop.
                Endloop
                */
                long rowperstrip = ifd.GetEntryRecursive((TagType)0x0116)?.GetLong(0) ?? throw new FormatException("File not correct");
                long strips = height / rowperstrip;
                long lastStrip = height % rowperstrip;
                var imageOffsetTag = ifd.GetEntryRecursive((TagType)0x0111) ?? throw new FormatException("File not correct");
                int cpp = ifd.GetEntryRecursive((TagType)0x0115)?.GetInt(0) ?? throw new FormatException("File not correct");
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
                            rawImage.raw.red[(y + i * rowperstrip) * width + x] = temp[x * 3];
                            //green
                            rawImage.raw.green[(y + i * rowperstrip) * width + x] = temp[x * 3 + 1];
                            //blue 
                            rawImage.raw.blue[(y + i * rowperstrip) * width + x] = temp[x * 3 + 2];
                            for (int z = 0; z < (cpp - 3); z++)
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
                rawImage.raw.ColorDepth = 8;
                using (var img = bitmapasync.Result)
                using (BitmapBuffer buffer = img.LockBuffer(BitmapBufferAccessMode.Write))
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    rawImage.raw.dim = new Point2D((uint)bufferLayout.Width, (uint)bufferLayout.Height);
                    rawImage.Init(true);
                    unsafe
                    {
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                        for (int y = 0; y < rawImage.raw.dim.height; y++)
                        {
                            long realY = y * rawImage.raw.dim.width;
                            long bufferY = y * rawImage.raw.dim.width * 4 + +bufferLayout.StartIndex;
                            for (int x = 0; x < rawImage.raw.dim.width; x++)
                            {
                                long realPix = realY + x;
                                long bufferPix = bufferY + (4 * x);
                                rawImage.raw.red[realPix] = temp[bufferPix + 2];
                                rawImage.raw.green[realPix] = temp[bufferPix + 1];
                                rawImage.raw.blue[realPix] = temp[bufferPix];
                            }
                        }
                    }
                }
            }
        }

        public override void DecodeMetadata()
        {
            if (rawImage.raw.ColorDepth == 0)
            {
                rawImage.raw.ColorDepth = ifd.GetEntryRecursive(TagType.BITSPERSAMPLE)?.GetUShort(0) ?? 0;
            }
            rawImage.metadata.IsoSpeed = ifd.GetEntryRecursive(TagType.ISOSPEEDRATINGS)?.GetInt(0) ?? 0;
            rawImage.metadata.Aperture = ifd.GetEntryRecursive(TagType.APERTUREVALUE)?.GetFloat(0) ?? ifd.GetEntryRecursive(TagType.FNUMBER)?.GetFloat(0) ?? 0;
            rawImage.metadata.Focal = ifd.GetEntryRecursive(TagType.FOCALLENGTH)?.GetDouble(0) ?? ifd.GetEntryRecursive(TagType.FOCALLENGTHIN35MMFILM)?.GetDouble(0) ?? 0;
            rawImage.metadata.Lens = ifd.GetEntryRecursive(TagType.LENSINFO)?.DataAsString;
            rawImage.metadata.Exposure = ifd.GetEntryRecursive(TagType.EXPOSURETIME)?.GetFloat(0) ?? 0;

            if (rawImage.whitePoint == 0)
            {
                rawImage.whitePoint = ifd.GetEntryRecursive(TagType.WHITELEVEL)?.GetUShort(0) ?? ifd.GetEntryRecursive(TagType.WHITEPOINT)?.GetUShort(0) ?? 0;

            }

            if (rawImage.black == 0)
            {
                rawImage.black = ifd.GetEntryRecursive(TagType.BLACKLEVEL)?.GetUShort(0) ?? 0;
            }

            rawImage.metadata.TimeTake = ifd.GetEntryRecursive(TagType.DATETIMEORIGINAL)?.DataAsString;
            rawImage.metadata.TimeModify = ifd.GetEntryRecursive(TagType.DATETIMEDIGITIZED)?.DataAsString;
            // Set the make and model
            rawImage.metadata.Make = ifd.GetEntryRecursive(TagType.MAKE)?.DataAsString.Trim();
            rawImage.metadata.Model = ifd.GetEntryRecursive(TagType.MODEL)?.DataAsString.Trim();

            rawImage.metadata.Comment = ifd.GetEntryRecursive(TagType.USERCOMMENT)?.DataAsString;
            rawImage.metadata.ColorSpace = (ColorSpaceType)(ifd.GetEntryRecursive(TagType.COLORSPACE)?.GetInt(0) ?? 0xffff);

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
                            rawImage.metadata.OriginalRotation = 2;
                            break;
                        case 4:
                        case 6:
                            rawImage.metadata.OriginalRotation = 1;
                            break;
                        case 7:
                        case 5:
                            rawImage.metadata.OriginalRotation = 3;
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
                        rawImage.metadata.OriginalRotation = 2;
                        break;
                    case 6:
                    case 5:
                        rawImage.metadata.OriginalRotation = 1;
                        break;
                    case 8:
                    case 7:
                        rawImage.metadata.OriginalRotation = 3;
                        break;
                }
            }
            rawImage.metadata.RawDim = new Point2D(rawImage.raw.UncroppedDim.width, rawImage.raw.UncroppedDim.height);
            try
            {
                //gps info
                var gps = ifd.GetIFDWithType(IFDType.GPS);
                if (gps != null)
                {
                    rawImage.metadata.Gps = new GPSInfo()
                    {
                        longitude = gps.GetEntry((TagType)0x04)?.GetAsDoubleArray() ?? new double[] { 0, 0, 0 },
                        lattitude = gps.GetEntry((TagType)0x02)?.GetAsDoubleArray() ?? new double[] { 0, 0, 0 },
                        longitudeRef = gps.GetEntry((TagType)0x03)?.DataAsString,
                        lattitudeRef = gps.GetEntry((TagType)0x01)?.DataAsString,
                        altitude = gps.GetEntry((TagType)0x06)?.GetFloat(0) ?? 0,
                        altitudeRef = gps.GetEntry((TagType)0x05)?.GetLong(0) ?? 0
                    };
                }
            }
            catch (Exception) { }
        }

        public override Thumbnail DecodeThumb()
        {
            List<IFD> previews;
            if ((previews = ifd.GetIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT))?.Count > 0)
            {
                //there is a jpeg preview
                if (previews?.Count == 0) return null;
                var preview = previews[0];
                var thumb = preview.GetEntry(TagType.JPEGINTERCHANGEFORMAT);
                var size = preview.GetEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
                if (size == null || thumb == null) return null;

                reader.Position = thumb.GetUInt(0);
                Thumbnail temp = new Thumbnail()
                {
                    data = reader.ReadBytes(size.GetInt(0)),
                    Type = ThumbnailType.JPEG,
                    dim = new Point2D()
                };
                return temp;
            }
            else
            {
                //find the preview IFD (usually the first if any)
                previews = ifd.GetIFDsWithTag(TagType.NEWSUBFILETYPE);
                IFD thumbIFD = null;
                if (previews?.Count != 0)
                {
                    for (int i = 0; i < previews.Count; i++)
                    {
                        var subFile = previews[i].GetEntry(TagType.NEWSUBFILETYPE);
                        if (subFile.GetInt(0) == 1)
                        {
                            thumbIFD = previews[i];
                            break;
                        }
                    }
                }
                if (thumbIFD == null) return null;

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

                    return new Thumbnail()
                    {
                        cpp = cpp,
                        dim = dim,
                        data = reader.ReadBytes(counts.GetInt(0)),
                        Type = ThumbnailType.RAW
                    };
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
                    return new Thumbnail()
                    {
                        data = reader.ReadBytes(size.GetInt(0)),
                        Type = ThumbnailType.JPEG,
                        dim = new Point2D()
                    };
                }
                else return null;
            }
        }

        protected void DecodeUncompressed(IFD rawIFD, BitOrder order)
        {
            uint nslices = rawIFD.GetEntry(TagType.STRIPOFFSETS).dataCount;
            Tag offsets = rawIFD.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = rawIFD.GetEntry(TagType.STRIPBYTECOUNTS);
            if (counts.dataCount != offsets.dataCount)
                throw new RawDecoderException("Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);

            uint yPerSlice = rawIFD.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
            uint width = rawIFD.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
            uint height = rawIFD.GetEntry(TagType.IMAGELENGTH).GetUInt(0);
            ushort bitPerPixel = rawIFD.GetEntry(TagType.BITSPERSAMPLE).GetUShort(0);
            rawImage.raw.ColorDepth = bitPerPixel;
            uint offY = 0;
            List<RawSlice> slices = new List<RawSlice>();
            for (int s = 0; s < nslices; s++)
            {
                RawSlice slice = new RawSlice()
                {
                    offset = offsets.GetUInt(s),
                    count = counts.GetUInt(s),
                    offsetY = offY
                };
                if (offY + yPerSlice > height)
                    slice.h = height - offY;
                else
                    slice.h = yPerSlice;

                offY += yPerSlice;

                if (reader.IsValid(slice.offset, slice.count)) // Only decode if size is valid
                    slices.Add(slice);
            }

            if (0 == slices.Count)
                throw new RawDecoderException("RAW Decoder: No valid slices found. File probably truncated.");

            rawImage.raw.dim = new Point2D(width, offY);
            rawImage.whitePoint = (ushort)((1 << bitPerPixel) - 1);

            offY = 0;
            for (int i = 0; i < slices.Count; i++)
            {
                RawSlice slice = slices[i];
                reader.BaseStream.Position = slice.offset;
                bitPerPixel = (ushort)(slice.count * 8u / (slice.h * width));
                RawDecompressor.ReadUncompressedRaw(reader, new Point2D(width, slice.h), new Point2D(0, slice.offsetY), rawImage.raw.cpp * width * bitPerPixel / 8, bitPerPixel, order, rawImage);
                offY += slice.h;
            }
        }
    }
}