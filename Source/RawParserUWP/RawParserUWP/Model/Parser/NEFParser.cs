using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RawParserUWP.Model.Format.Base;
using RawParserUWP.Model.Format.Image;
using RawParserUWP.Model.Parser.Nikon;
using RawParserUWP.Model.Format.Reader;

namespace RawParserUWP.Model.Parser
{
    class NEFParser : Parser,IDisposable
    {
        protected BinaryReader fileStream;
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
            currentRawImage.previewImage =parsePreview();

            //read the exif
            currentRawImage.exif = parseExif();
            //read the data
            currentRawImage.imageData = parseRAWImage();
            return currentRawImage;
        }

        public override void setStream(Stream file) 
        {
            //Open a binary stream on the file
            fileStream = new BinaryReader(file);

            //read the first bit to get the endianness of the file           
            if (fileStream.ReadUInt16() == 0x4D4D)
            {
                //File is in reverse bit order
               // fileStream.Dispose(); //DO NOT dispose, because it remove the filestream not the reader and crash the parse
                fileStream = new BinaryReaderBE(file);
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
            Tag imageRAWWidth, imageRAWHeight,imageRAWDepth;
            if (!subifd1.tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
            colorDepth = (ushort)imageRAWDepth.data[0];
            height = (uint)imageRAWHeight.data[0];
            width = (uint)imageRAWWidth.data[0];
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

        public override BitArray parseRAWImage()
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
           
            BitArray rawData;
            //Check if uncompressed
            if ((ushort)imageRAWCompressed.data[0] == 34713)
            {
                //uncompress the image
                LinearisationTable line = new LinearisationTable((ushort)compressionType.data[0], 
                    (ushort)imageRAWDepth.data[0], (uint)imageRAWOffsetTags.data[0],
                    lineTag.dataOffset + makerNote.getOffset(), fileStream);

                makerNote = null;
                rawData = line.uncompressed(height, width);
                line.Dispose();
            }
            else
            {
                //get Raw Data            
                fileStream.BaseStream.Position = (uint)imageRAWOffsetTags.data[0];
                rawData = new BitArray(fileStream.ReadBytes(Convert.ToInt32(imageRAWSize.data[0])));
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
