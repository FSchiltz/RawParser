using RawParser.Model.Format;
using RawParser.Model.Format.Nikon;
using RawParser.Model.ImageDisplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace RawParser.Model.Parser
{
    class NEFParser : Parser
    {
        protected IFD ifd;
        protected Header header;
        protected IFD subifd0;
        protected IFD subifd1;
        protected IFD exif;
        protected NikonMakerNote makerNote;

        protected BitArray rawData;
        protected byte[] previewData;

        public RawImage parse(Stream file)
        {
            //Open a binary stream on the file
            BinaryReader fileStream = new BinaryReader(file);

            //read the first bit to get the endianness of the file           
            if (fileStream.ReadUInt16() == 0x4D4D)
            {
                //File is in reverse bit order
                fileStream = new BinaryReaderBE(file);
            }

            //read the header
            header = new Header(fileStream, 0); // OK

            //Read the IFD
            ifd = new IFD(fileStream, header.TIFFoffset, true); // OK

            Tag subifdoffsetTag;
            Tag exifoffsetTag;
            if (!ifd.tags.TryGetValue(0x14A, out subifdoffsetTag)) throw new FormatException("File not correct");
            if (!ifd.tags.TryGetValue(0x8769, out exifoffsetTag)) throw new FormatException("File not correct");

            subifd0 = new IFD(fileStream, (uint)subifdoffsetTag.data[0], true);
            subifd1 = new IFD(fileStream, (uint)subifdoffsetTag.data[1], true);
            //todo third IFD
            exif = new IFD(fileStream, (uint)exifoffsetTag.data[0], true); //OK

            //optimize (stop ifd from loaoding the makernote

            Tag makerNoteOffsetTag;
            if (!exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag)) throw new FormatException("File not correct");

            makerNote = new NikonMakerNote(fileStream, makerNoteOffsetTag.dataOffset, true);

            //Get image data
            Tag imagepreviewOffsetTags, imagepreviewX, imagepreviewY, imagepreviewSize;
            if (!subifd0.tags.TryGetValue(0x201, out imagepreviewOffsetTags)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11A, out imagepreviewX)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11B, out imagepreviewY)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x202, out imagepreviewSize)) throw new FormatException("File not correct");

            Tag imageRAWOffsetTags, imageRAWWidth, imageRAWHeight, imageRAWSize, imageRAWCompressed, imageRAWDepth;
            if (!subifd1.tags.TryGetValue(0x0111, out imageRAWOffsetTags)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0117, out imageRAWSize)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0117, out imageRAWCompressed)) throw new FormatException("File not correct");

            //get the preview data ( faster than rezising )
            fileStream.BaseStream.Position = (uint)imagepreviewOffsetTags.data[0];
            previewData = fileStream.ReadBytes(Convert.ToInt32(imagepreviewSize.data[0]));

            //get Raw Data            
            fileStream.BaseStream.Position = (uint)imageRAWOffsetTags.data[0];
            rawData = new BitArray(fileStream.ReadBytes(Convert.ToInt32(imageRAWSize.data[0])));

            //Check if uncompressed
            if ((uint)imageRAWCompressed.data[0] == 34713)
            {
                Tag compressionType;
                if (!makerNote.ifd.tags.TryGetValue(0x0093, out compressionType)) throw new FormatException("File not correct");
                //uncompress the image
                rawData = uncompressed(new BitArray(rawData), (int)imageRAWHeight.data[0], (int)imageRAWWidth.data[0], (ushort)compressionType.data[0], (ushort)imageRAWDepth.data[0]);
            }
            //parse to RawImage
            Dictionary<ushort, Tag> exifTag = parseToStandardExifTag();
            RawImage rawImage = new RawImage(exifTag, rawData, previewData, (uint)imageRAWHeight.data[0], (uint)imageRAWWidth.data[0], (ushort)imageRAWDepth.data[0]);

            return rawImage;
        }

        /*
         * Only lossless for the moment
         * 
         */
        private BitArray uncompressed(BitArray rawData, int height, int width, ushort compressionType, ushort colordepth)
        {
            byte[] uncompressedData = new byte[height * width]; //add pixel*
            //decompress the linearisationtable
            Tag lineTag = new Tag();
            makerNote.ifd.tags.TryGetValue(0x0096, out lineTag);
            LinearisationTable line = new LinearisationTable(lineTag.data, compressionType);

            //huffman tree for the different copression type
            byte[][] nikon_tree =
                {
                    new byte[]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy */
                      5,4,3,6,2,7,1,0,8,9,11,10,12 },
                    new byte[]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy after split */
                      0x39,0x5a,0x38,0x27,0x16,5,4,3,2,1,0,11,12,12 },
                    new byte[] { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,  /* 12-bit lossless */
                      5,4,6,3,7,2,8,1,9,0,10,11,12 },
                    new byte[]{ 0,1,4,3,1,1,1,1,1,2,0,0,0,0,0,0,	/* 14-bit lossy */
                      5,6,4,7,8,3,9,2,1,0,10,11,12,13,14 },
                    new byte[]{ 0,1,5,1,1,1,1,1,1,1,2,0,0,0,0,0,	/* 14-bit lossy after split */
                      8,0x5c,0x4b,0x3a,0x29,7,6,5,4,3,2,1,0,13,14 },
                    new byte [] { 0,1,4,2,2,3,1,2,0,0,0,0,0,0,0,0,	/* 14-bit lossless */
                      7,6,8,5,9,4,10,3,11,12,2,0,1,13,14 }
            };

            switch (compressionType)
            {
                case 1:
                    //lossy
                    {
                    }
                    break;
                case 3:
                    //Lossless
                    {

                    }
                    break;
                case 4:
                    //Compression(Compression = 34713) is a Huffman tree and a quantization table. The quantization tables are at 0x8c and 0x96 tag from the MakerNote.
                    //lossy type2
                    {
                    }
                    break;
                default: throw new FormatException("Comrpession Type not correct");
            }
            return rawData;
        }

        public Dictionary<ushort, Tag> parseToStandardExifTag()
        {
            Dictionary<ushort, Tag> temp = new Dictionary<ushort, Tag>();
            Dictionary<ushort, ushort> nikonToStandard = new DictionnaryFromFileUShort(@"Assets\Dic\NikonToStandard.dic");
            Dictionary<ushort, string> standardExifName = new DictionnaryFromFileString(@"Assets\\Dic\StandardExif.dic");
            foreach (ushort exifTag in standardExifName.Keys)
            {
                Tag tempTag;
                ushort nikonTagId;
                if (nikonToStandard.TryGetValue(exifTag, out nikonTagId))
                {
                    ifd.tags.TryGetValue(nikonTagId, out tempTag);
                    makerNote.ifd.tags.TryGetValue(nikonTagId, out tempTag);
                    subifd0.tags.TryGetValue(nikonTagId, out tempTag);
                    subifd1.tags.TryGetValue(nikonTagId, out tempTag);
                    if (tempTag == null)
                    {
                        tempTag = new Tag();
                        tempTag.dataType = 2;
                        tempTag.data[0] = "";
                    }
                    string t = "";
                    standardExifName.TryGetValue(exifTag, out t);
                    tempTag.displayName = t;

                    temp.Add(nikonTagId, tempTag);
                }
            }
            return temp;
        }
    }
}
