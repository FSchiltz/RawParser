using System;
using System.Collections.Generic;
using System.IO;
using RawParserUWP.Model.Format.Base;
using RawParserUWP.Model.Format.Image;
using RawParserUWP.Model.Parser.Nikon;
using RawParserUWP.Model.Format.Reader;

namespace RawParserUWP.Model.Parser
{
    class NEFParser : Parser, IDisposable
    {
        protected TIFFBinaryReader fileStream;
        protected IFD ifd, subifd0, subifd1, exif;
        protected NikonMakerNote makerNote;
        protected Header header;

        public override RawImage parse(Stream s)
        {
            RawImage currentRawImage = new RawImage();
            //Set the stream
            setStream(s);

            //read the thumbnail
            currentRawImage.thumbnail = parseThumbnail();

            //read the preview
            currentRawImage.previewImage = parsePreview();

            //read the exif
            currentRawImage.exif = parseExif();
            //read the data
            currentRawImage.imageData = parseRAWImage();
            return currentRawImage;
        }

        public override void setStream(Stream file)
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

        public override byte[] parseThumbnail()
        {
            //get the Exif
            Tag exifoffsetTag;
            if (!ifd.tags.TryGetValue(0x8769, out exifoffsetTag)) throw new FormatException("File not correct");
            //todo third IFD
            exif = new IFD(fileStream, (uint)exifoffsetTag.data[0], true, false);
            Tag makerNoteOffsetTag;
            if (!exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag)) throw new FormatException("File not correct");
            makerNote = new NikonMakerNote(fileStream, makerNoteOffsetTag.dataOffset, true);
            Tag thumbnailOffset, thumbnailSize;
            if (!makerNote.preview.tags.TryGetValue(0x0201, out thumbnailOffset)) throw new FormatException("File not correct");
            if (!makerNote.preview.tags.TryGetValue(0x0202, out thumbnailSize)) throw new FormatException("File not correct");
            //get the preview data ( faster than rezising )
            fileStream.BaseStream.Position = (uint)(thumbnailOffset.data[0]) + 10 + (uint)(makerNoteOffsetTag.dataOffset);
            return fileStream.ReadBytes(Convert.ToInt32(thumbnailSize.data[0]));
        }

        public override byte[] parsePreview()
        {
            //Get the full size preview
            Tag subifdoffsetTag;
            if (!ifd.tags.TryGetValue(0x14A, out subifdoffsetTag)) throw new FormatException("File not correct");
            subifd0 = new IFD(fileStream, (uint)subifdoffsetTag.data[0], true, false);
            subifd1 = new IFD(fileStream, (uint)subifdoffsetTag.data[1], true, false);
            Tag imagepreviewOffsetTags, imagepreviewX, imagepreviewY, imagepreviewSize;
            if (!subifd0.tags.TryGetValue(0x201, out imagepreviewOffsetTags)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11A, out imagepreviewX)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11B, out imagepreviewY)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x202, out imagepreviewSize)) throw new FormatException("File not correct");

            //get the preview data ( faster than rezising )
            fileStream.BaseStream.Position = (uint)imagepreviewOffsetTags.data[0];
            return fileStream.ReadBytes(Convert.ToInt32(imagepreviewSize.data[0]));
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            //Get the RAW data info
            Tag imageRAWWidth, imageRAWHeight, imageRAWDepth, imageRAWCFA;
            if (!subifd1.tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x828e, out imageRAWCFA)) throw new FormatException("File not correct");
            colorDepth = (ushort)imageRAWDepth.data[0];
            height = (uint)imageRAWHeight.data[0];
            width = (uint)imageRAWWidth.data[0];
            cfa = new byte[4];
            for (int i = 0; i < 4; i++) cfa[i] = (byte)imageRAWCFA.data[i];

            //get the colorBalance
            Tag colorBalanceTag, colorLevelTag;
            //first get the matrix of level for each pixel (a 2*2 array corresponding to the rgb bayer matrice used            
            if (!makerNote.ifd.tags.TryGetValue(0xc, out colorLevelTag)) throw new FormatException("File not correct");
            for (int c = 0; c < 3; c++)
            {
                camMul[((c << 1) | (c >> 1)) & 3] = (double)colorLevelTag.data[c];
            }

            //then get the R and B multiplier           
            if (!makerNote.ifd.tags.TryGetValue(0x97, out colorBalanceTag)) throw new FormatException("File not correct");
            int version = 0;
            for (int i = 0; i < 4; i++)
                version = version * 10 + (byte)(colorBalanceTag.data[i]) - '0';
            if (version < 200)
            {
                switch (version)
                {
                    case 100:
                        for (int c = 0; c < 4; c++) camMul[(c >> 1) | ((c & 1) << 1)] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 68);
                        break;
                    case 102:
                        for (int c = 0; c < 4; c++) camMul[c ^ (c >> 1)] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 6);
                        //check
                        //for (int c = 0; c < 4; c++) sraw_mul[c ^ (c >> 1)] = get2();
                        break;
                    case 103:
                        for (int c = 0; c < 4; c++) camMul[c] = fileStream.readshortFromArrayC(ref colorBalanceTag.data, (c * 2) + 16);
                        break;
                }
            }
            else
            {
                //encrypted
                byte[][] xlat = new byte[2][] {
                    new byte [256]{ 0xc1,0xbf,0x6d,0x0d,0x59,0xc5,0x13,0x9d,0x83,0x61,0x6b,0x4f,0xc7,0x7f,0x3d,0x3d,
                      0x53,0x59,0xe3,0xc7,0xe9,0x2f,0x95,0xa7,0x95,0x1f,0xdf,0x7f,0x2b,0x29,0xc7,0x0d,
                      0xdf,0x07,0xef,0x71,0x89,0x3d,0x13,0x3d,0x3b,0x13,0xfb,0x0d,0x89,0xc1,0x65,0x1f,
                      0xb3,0x0d,0x6b,0x29,0xe3,0xfb,0xef,0xa3,0x6b,0x47,0x7f,0x95,0x35,0xa7,0x47,0x4f,
                      0xc7,0xf1,0x59,0x95,0x35,0x11,0x29,0x61,0xf1,0x3d,0xb3,0x2b,0x0d,0x43,0x89,0xc1,
                      0x9d,0x9d,0x89,0x65,0xf1,0xe9,0xdf,0xbf,0x3d,0x7f,0x53,0x97,0xe5,0xe9,0x95,0x17,
                      0x1d,0x3d,0x8b,0xfb,0xc7,0xe3,0x67,0xa7,0x07,0xf1,0x71,0xa7,0x53,0xb5,0x29,0x89,
                      0xe5,0x2b,0xa7,0x17,0x29,0xe9,0x4f,0xc5,0x65,0x6d,0x6b,0xef,0x0d,0x89,0x49,0x2f,
                      0xb3,0x43,0x53,0x65,0x1d,0x49,0xa3,0x13,0x89,0x59,0xef,0x6b,0xef,0x65,0x1d,0x0b,
                      0x59,0x13,0xe3,0x4f,0x9d,0xb3,0x29,0x43,0x2b,0x07,0x1d,0x95,0x59,0x59,0x47,0xfb,
                      0xe5,0xe9,0x61,0x47,0x2f,0x35,0x7f,0x17,0x7f,0xef,0x7f,0x95,0x95,0x71,0xd3,0xa3,
                      0x0b,0x71,0xa3,0xad,0x0b,0x3b,0xb5,0xfb,0xa3,0xbf,0x4f,0x83,0x1d,0xad,0xe9,0x2f,
                      0x71,0x65,0xa3,0xe5,0x07,0x35,0x3d,0x0d,0xb5,0xe9,0xe5,0x47,0x3b,0x9d,0xef,0x35,
                      0xa3,0xbf,0xb3,0xdf,0x53,0xd3,0x97,0x53,0x49,0x71,0x07,0x35,0x61,0x71,0x2f,0x43,
                      0x2f,0x11,0xdf,0x17,0x97,0xfb,0x95,0x3b,0x7f,0x6b,0xd3,0x25,0xbf,0xad,0xc7,0xc5,
                      0xc5,0xb5,0x8b,0xef,0x2f,0xd3,0x07,0x6b,0x25,0x49,0x95,0x25,0x49,0x6d,0x71,0xc7 },
                    new byte [256]{ 0xa7,0xbc,0xc9,0xad,0x91,0xdf,0x85,0xe5,0xd4,0x78,0xd5,0x17,0x46,0x7c,0x29,0x4c,
                      0x4d,0x03,0xe9,0x25,0x68,0x11,0x86,0xb3,0xbd,0xf7,0x6f,0x61,0x22,0xa2,0x26,0x34,
                      0x2a,0xbe,0x1e,0x46,0x14,0x68,0x9d,0x44,0x18,0xc2,0x40,0xf4,0x7e,0x5f,0x1b,0xad,
                      0x0b,0x94,0xb6,0x67,0xb4,0x0b,0xe1,0xea,0x95,0x9c,0x66,0xdc,0xe7,0x5d,0x6c,0x05,
                      0xda,0xd5,0xdf,0x7a,0xef,0xf6,0xdb,0x1f,0x82,0x4c,0xc0,0x68,0x47,0xa1,0xbd,0xee,
                      0x39,0x50,0x56,0x4a,0xdd,0xdf,0xa5,0xf8,0xc6,0xda,0xca,0x90,0xca,0x01,0x42,0x9d,
                      0x8b,0x0c,0x73,0x43,0x75,0x05,0x94,0xde,0x24,0xb3,0x80,0x34,0xe5,0x2c,0xdc,0x9b,
                      0x3f,0xca,0x33,0x45,0xd0,0xdb,0x5f,0xf5,0x52,0xc3,0x21,0xda,0xe2,0x22,0x72,0x6b,
                      0x3e,0xd0,0x5b,0xa8,0x87,0x8c,0x06,0x5d,0x0f,0xdd,0x09,0x19,0x93,0xd0,0xb9,0xfc,
                      0x8b,0x0f,0x84,0x60,0x33,0x1c,0x9b,0x45,0xf1,0xf0,0xa3,0x94,0x3a,0x12,0x77,0x33,
                      0x4d,0x44,0x78,0x28,0x3c,0x9e,0xfd,0x65,0x57,0x16,0x94,0x6b,0xfb,0x59,0xd0,0xc8,
                      0x22,0x36,0xdb,0xd2,0x63,0x98,0x43,0xa1,0x04,0x87,0x86,0xf7,0xa6,0x26,0xbb,0xd6,
                      0x59,0x4d,0xbf,0x6a,0x2e,0xaa,0x2b,0xef,0xe6,0x78,0xb6,0x4e,0xe0,0x2f,0xdc,0x7c,
                      0xbe,0x57,0x19,0x32,0x7e,0x2a,0xd0,0xb8,0xba,0x29,0x00,0x3c,0x52,0x7d,0xa8,0x49,
                      0x3b,0x2d,0xeb,0x25,0x49,0xfa,0xa3,0xaa,0x39,0xa7,0xc5,0xa7,0x50,0x11,0x36,0xfb,
                      0xc6,0x67,0x4a,0xf5,0xa5,0x12,0x65,0x7e,0xb0,0xdf,0xaf,0x4e,0xb3,0x61,0x7f,0x2f } };
                Tag serialTag, shutterCountTag;
                if (!makerNote.ifd.tags.TryGetValue(0x1D, out serialTag)) throw new FormatException("File not correct");
                if (!makerNote.ifd.tags.TryGetValue(0xA7, out shutterCountTag)) throw new FormatException("File not correct");
                byte[] buff = new byte[324];
                for (int i = 0; i < 324; i++)
                {
                    buff[i] = (byte)colorBalanceTag.data[i + 1];
                }
                
                //get serial
                int serial = 0;
                for (int i = 0; i < serialTag.data.Length; i++)
                {
                    byte c = (byte)serialTag.dataAsString[i];
                    serial = (byte)(serial * 10) + (char.IsDigit((char)c) ? c - '0' : c % 10);
                }
                if (version < 217)
                {
                    byte ci, cj, ck;
                    ci = xlat[0][serial & 0xff];
                    byte[] shutterAsByte = BitConverter.GetBytes((uint)shutterCountTag.data[0]);
                    cj = xlat[1][shutterAsByte[0] ^ shutterAsByte[1] ^ shutterAsByte[2] ^ shutterAsByte[3]];
                    ck = 0x60;
                    for (int i = 0; i < 324; i++)
                    {
                        buff[i] ^= (cj += (byte)(ci * ck++));
                    }
                    int offset = "66666>666;6A;:;55"[version - 200] - '0';
                    for (int c = 0; c < 4; c++)
                    {
                        camMul[c ^ (c >> 1) ^ (offset & 1)] = fileStream.readUshortFromArray(ref buff, (offset & -2) + c * 2);
                    }
                }
            }

            Dictionary<ushort, Tag> temp = new Dictionary<ushort, Tag>();
            Dictionary<ushort, ushort> nikonToStandard = new DictionnaryFromFileUShort(@"Assets\Dic\NikonToStandard.dic");
            Dictionary<ushort, string> standardExifName = new DictionnaryFromFileString(@"Assets\\Dic\StandardExif.dic");
            foreach (ushort exifTag in standardExifName.Keys)
            {
                Tag tempTag;
                ushort nikonTagId;
                if (!nikonToStandard.TryGetValue(exifTag, out nikonTagId)) continue;
                ifd.tags.TryGetValue(nikonTagId, out tempTag);
                subifd0.tags.TryGetValue(nikonTagId, out tempTag);
                subifd1.tags.TryGetValue(nikonTagId, out tempTag);
                makerNote.preview.tags.TryGetValue(nikonTagId, out tempTag);
                makerNote.ifd.tags.TryGetValue(nikonTagId, out tempTag);
                exif.tags.TryGetValue(nikonTagId, out tempTag);
                if (tempTag == null)
                {
                    tempTag = new Tag
                    {
                        dataType = 2,
                        data = { [0] = "" }
                    };
                }
                string t = "";
                standardExifName.TryGetValue(exifTag, out t);
                tempTag.displayName = t;

                temp.Add(nikonTagId, tempTag);
            }

            return temp;
        }

        public override ushort[] parseRAWImage()
        {
            //Get the RAW data info
            Tag imageRAWOffsetTags, imageRAWWidth, imageRAWHeight, imageRAWSize, imageRAWCompressed, imageRAWDepth;
            if (!subifd1.tags.TryGetValue(0x0111, out imageRAWOffsetTags)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0117, out imageRAWSize)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0103, out imageRAWCompressed)) throw new FormatException("File not correct");

            //decompress the linearisationtable
            Tag lineTag;
            if (!makerNote.ifd.tags.TryGetValue(0x0096, out lineTag)) throw new FormatException("File not correct");

            Tag compressionType;
            if (!makerNote.ifd.tags.TryGetValue(0x0093, out compressionType)) throw new FormatException("File not correct");

            //Free all the ifd
            ifd = null;
            subifd0 = null;
            subifd1 = null;

            header = null;

            ushort[] rawData;
            //Check if uncompressed
            if ((ushort)imageRAWCompressed.data[0] == 34713)
            {
                //uncompress the image
                LinearisationTable line = new LinearisationTable((ushort)compressionType.data[0],
                    (ushort)imageRAWDepth.data[0], (uint)imageRAWOffsetTags.data[0],
                    lineTag.dataOffset + makerNote.getOffset(), fileStream);

                makerNote = null;
                rawData = line.uncompressed(height, width, cfa);
                line.Dispose();
            }
            else
            {
                //get Raw Data            
                fileStream.BaseStream.Position = (uint)imageRAWOffsetTags.data[0];
                //TODO convert toushort from the byte table
                //Normaly only nikon camera between D1 and d100 are not compressed
                fileStream.ReadBytes(Convert.ToInt32(imageRAWSize.data[0]));
                rawData = null;
            }
            fileStream.Dispose();
            return rawData;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).

                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~NEFParser() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
