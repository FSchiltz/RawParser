using RawParser.Model.Format;
using RawParser.Model.Format.Nikon;
using RawParser.Model.ImageDisplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        protected byte[] thumbnail;

        public RawImage parse(Stream file)
        {
            byte[] previewData;
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
            ifd = new IFD(fileStream, header.TIFFoffset, true, false); // OK

            //TODO
            //Read the thumbnail
            //Callback

            //Get the full size preview
            Tag subifdoffsetTag;
            if (!ifd.tags.TryGetValue(0x14A, out subifdoffsetTag)) throw new FormatException("File not correct");
            subifd0 = new IFD(fileStream, (uint)subifdoffsetTag.data[0], true, false);
            Tag imagepreviewOffsetTags, imagepreviewX, imagepreviewY, imagepreviewSize;
            if (!subifd0.tags.TryGetValue(0x201, out imagepreviewOffsetTags)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11A, out imagepreviewX)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x11B, out imagepreviewY)) throw new FormatException("File not correct");
            if (!subifd0.tags.TryGetValue(0x202, out imagepreviewSize)) throw new FormatException("File not correct");

            //get the preview data ( faster than rezising )
            fileStream.BaseStream.Position = (uint)imagepreviewOffsetTags.data[0];
            previewData = fileStream.ReadBytes(Convert.ToInt32(imagepreviewSize.data[0]));

            //TODO
            //CallBack

            previewData = null; //free memory

            //get the Exif
            Tag exifoffsetTag;
            if (!ifd.tags.TryGetValue(0x8769, out exifoffsetTag)) throw new FormatException("File not correct");
            subifd1 = new IFD(fileStream, (uint)subifdoffsetTag.data[1], true, false);
            //todo third IFD
            exif = new IFD(fileStream, (uint)exifoffsetTag.data[0], true, false);
            Tag makerNoteOffsetTag;
            if (!exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag)) throw new FormatException("File not correct");
            makerNote = new NikonMakerNote(fileStream, makerNoteOffsetTag.dataOffset, true);
            Dictionary<ushort, Tag> exifTag = parseToStandardExifTag();
            //TODO
            //callback for exif


            //Get the RAW data info
            Tag imageRAWOffsetTags, imageRAWWidth, imageRAWHeight, imageRAWSize, imageRAWCompressed, imageRAWDepth;
            if (!subifd1.tags.TryGetValue(0x0111, out imageRAWOffsetTags)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0100, out imageRAWWidth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0101, out imageRAWHeight)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0102, out imageRAWDepth)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0117, out imageRAWSize)) throw new FormatException("File not correct");
            if (!subifd1.tags.TryGetValue(0x0103, out imageRAWCompressed)) throw new FormatException("File not correct");
            //get Raw Data            
            fileStream.BaseStream.Position = (uint)imageRAWOffsetTags.data[0];
            rawData = new BitArray(fileStream.ReadBytes(Convert.ToInt32(imageRAWSize.data[0])));

            //Check if uncompressed
            if ((ushort)imageRAWCompressed.data[0] == 34713)
            {                
                rawData = uncompressed(new BitArray(rawData), (uint)imageRAWHeight.data[0], (uint)imageRAWWidth.data[0], (ushort)imageRAWDepth.data[0], (uint)imageRAWOffsetTags.data[0], fileStream);
            }
          
            RawImage rawImage = new RawImage(exifTag, rawData, thumbnail, (uint)imageRAWHeight.data[0], (uint)imageRAWWidth.data[0], (ushort)imageRAWDepth.data[0]);

            fileStream.Dispose();
            return rawImage;
        }

        /*
         * First for 14bit lossless
         * 
         * ver0 = 70 (0x48)
         * ver1 = 48 (0x30)
         * 
         * From DCraw
         * Not working
         * 
         */
        private BitArray uncompressed(BitArray rawData, uint height, uint width, ushort colordepth, uint dataoffset, BinaryReader reader)
        {
            BitArray uncompressedData = new BitArray((int)(height * width * colordepth)); //add pixel*
            //decompress the linearisationtable
            Tag lineTag = new Tag();
            makerNote.ifd.tags.TryGetValue(0x0096, out lineTag);
            Tag compressionType;
            if (!makerNote.ifd.tags.TryGetValue(0x0093, out compressionType)) throw new FormatException("File not correct");
            //uncompress the image<
            LinearisationTable line = new LinearisationTable(lineTag.data, (ushort)compressionType.data[0], colordepth, dataoffset, reader);
            return line.uncompressed(rawData,height, width, colordepth);
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
                    exif.tags.TryGetValue(nikonTagId, out tempTag);
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
