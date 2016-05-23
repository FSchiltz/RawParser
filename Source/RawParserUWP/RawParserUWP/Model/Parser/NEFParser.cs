using RawParser.Model.Format;
using RawParser.Model.Format.Nikon;
using RawParser.Model.ImageDisplay;
using System;
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

        protected byte[] rawData;
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
            ifd.tags.TryGetValue(0x14A, out subifdoffsetTag);
            ifd.tags.TryGetValue(0x8769, out exifoffsetTag);

            subifd0 = new IFD(fileStream, (uint)subifdoffsetTag.data[0], true);
            subifd1 = new IFD(fileStream, (uint)subifdoffsetTag.data[1], true);
            //todo third IFD
            exif = new IFD(fileStream, (uint)exifoffsetTag.data[0], true); //OK

            //optimize (stop ifd from loaoding the makernote
            
            Tag makerNoteOffsetTag;
            exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag);
                       
            makerNote = new NikonMakerNote(fileStream, makerNoteOffsetTag.dataOffset, true);

            //Get image data
            Tag imagepreviewOffsetTags,imagepreviewX,imagepreviewY,imagepreviewSize;
            makerNote.preview.tags.TryGetValue(0x201,out imagepreviewOffsetTags );
            makerNote.preview.tags.TryGetValue(0x11A, out imagepreviewX);
            makerNote.preview.tags.TryGetValue(0x11B, out imagepreviewY);
            makerNote.preview.tags.TryGetValue(0x202, out imagepreviewSize);

            //get Preview Data
           
            //get Raw Data
            rawData = new byte[0];

            //get the preview data ( faster than rezising )
            fileStream.BaseStream.Position = (uint)imagepreviewOffsetTags.data[0] + makerNoteOffsetTag.dataOffset + 10;
            previewData = fileStream.ReadBytes(Convert.ToInt32(imagepreviewSize.data[0]));
            

            //parse to RawImage
            Dictionary<ushort, Tag>exifTag = parseToStandardExifTag();
            RawImage rawImage = new RawImage(exifTag, rawData, previewData);
            //get the imagedata

            return rawImage;
        }

        public Dictionary<ushort, Tag> parseToStandardExifTag()
        {
            Dictionary<ushort, Tag> temp = new Dictionary<ushort, Tag>();
            Dictionary<ushort, ushort> nikonToStandard = new NikonTagDictionnary();
            Dictionary<ushort, string> standardExifName = new StandardExifNameDictionnary();
            foreach(ushort nikonTagId in nikonToStandard.Values)
            {
                Tag tempTag = null;
                if(ifd.tags.TryGetValue(nikonTagId, out tempTag))
                {

                }
                else if (makerNote.ifd.tags.TryGetValue(nikonTagId, out tempTag)) //search in MakerNote
                {

                }
                else if (subifd0.tags.TryGetValue(nikonTagId, out tempTag))
                {

                }
                else if (subifd1.tags.TryGetValue(nikonTagId, out tempTag))
                {

                }else
                {
                    continue;
                }
                //tag is found
            }
            return temp;
        }
    }
}
