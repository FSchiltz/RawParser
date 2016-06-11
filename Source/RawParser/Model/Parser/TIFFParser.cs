using RawParser.Format.IFD;
using RawParser.Image;
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

        internal void readTiffBase(Stream file)
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


        public override void Parse(Stream file)
        {
            readTiffBase(file);
            Tag PhotometricInterpretationTag;
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            throw new NotImplementedException();
        }

        public override byte[] parsePreview()
        {
            return null;
        }

        public override ushort[] parseRAWImage()
        {
            Tag imageOffsetTagsTag, imageWidthTag, imageHeightTag, imageCompressedTag, photoMetricTag,RowPerStripTag,stripSizeTag;            
            
            if (!ifd.tags.TryGetValue(0x0111, out imageOffsetTags)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0100, out imageWidth)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0101, out imageHeight)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0102, out imageDepth)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0117, out imageSize)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x0103, out imageCompressed)) throw new FormatException("File not correct");
        }

        public override byte[] parseThumbnail()
        {
            return null;
        }
    }
}
