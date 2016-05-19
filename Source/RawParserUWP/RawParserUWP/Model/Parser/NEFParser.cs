using RawParser.Model.Format;
using RawParser.Model.Format.Base;
using RawParser.Model.Format.Nikon;
using RawParser.Model.ImageDisplay;
using System.Collections.Generic;
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

        protected Image rawData;
        protected Image previewData;

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

            MemoryStream ms = new MemoryStream();
          
            Tag makerNoteOffsetTag;
            exif.tags.TryGetValue(0x927C, out makerNoteOffsetTag);
            object[] binMakerNoteObj = makerNoteOffsetTag.data;
            byte[] binMakerNote = binMakerNoteObj.Cast<byte>().ToArray();
            ms.Write(binMakerNote, 10, binMakerNote.Length - 10);
            ms.Position = 0; //reset the stream after populate

            makerNote = new NikonMakerNote(new BinaryReader(ms), 0, true);

            //Get image data
            Tag imagepreviewOffsetTags,imagepreviewX,imagepreviewY,imagepreviewSize;
            makerNote.preview.tags.TryGetValue(0x201,out imagepreviewOffsetTags);
            makerNote.preview.tags.TryGetValue(0x11A, out imagepreviewX);
            makerNote.preview.tags.TryGetValue(0x11B, out imagepreviewY);
            makerNote.preview.tags.TryGetValue(0x202, out imagepreviewSize);

            //get Preview Data
            rawData = new Image(0, 0, 0, true,fileStream, 0);
            //get Raw Data

            //parse to RawImage
            Dictionary<ushort, Tag>exifTag = parseToStandardExifTag();
            RawImage rawImage = new RawImage(exifTag, rawData, previewData);
            //get the imagedata

            rawImage.imageData = new Image(
                );

            //get the preview data ( faster than rezising )
            rawImage.imagePreviewData = new Image(
                (double)imagepreviewX.data[0],
                (double)imagepreviewY.data[0],
                (uint)imagepreviewSize.data[0],
                false,
                fileStream,
                (uint)imagepreviewOffsetTags.data[0]);
            return rawImage;
        }

        public Dictionary<ushort, Tag> parseToStandardExifTag()
        {
            Dictionary<ushort, Tag> temp = new Dictionary <ushort,Tag>();
            
            return temp;
        }
    }
}
