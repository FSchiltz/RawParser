using RawParser.Model.Format;
using RawParser.Model.Format.Base;
using RawParser.Model.Format.Image.Base;
using RawParser.Model.Format.Image.IFD;
using RawParser.Model.ImageDisplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RawParser.Model.Parser
{
    class NEFParser : TIFFParser
    {
        protected NEFIFD ifd;

        protected NEFIFD subifd0; 
        protected NEFIFD subifd1;
        protected NEFIFD exif;
        protected NikonMakerNote makerNote;

        protected Image rawData;
        protected Image previewData;

        override public RawImage parse(string path)
        {
            BinaryReader fileStream = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
            try
            {                
                Header header = new Header(fileStream, 0);
                if (header.byteOrder == 0x4D4D)
                {
                    //File is in reverse bit order
                    fileStream = new BinaryReaderBE(new FileStream(path, FileMode.Open, FileAccess.Read));
                    header = new Header(fileStream, 0);
                }
                ifd = new NEFIFD(fileStream, header.TIFFoffset,  true);

                Tag subifdoffsetTag ;
                Tag exifoffsetTag ;
                ifd.tags.TryGetValue(330,out subifdoffsetTag);
                ifd.tags.TryGetValue(34665,out exifoffsetTag);

                subifd0 = new NEFIFD(fileStream, (uint)subifdoffsetTag.data[0], true);
                subifd1 = new NEFIFD(fileStream, (uint)subifdoffsetTag.data[1],  true);
                exif = new NEFIFD(fileStream, (uint)exifoffsetTag.data[0], true);

                MemoryStream ms = new MemoryStream();
                MemoryStream headerms = new MemoryStream();
                Tag makerNoteOffsetTag;
                exif.tags.TryGetValue(37500, out makerNoteOffsetTag);
                object[] binMakerNoteObj = makerNoteOffsetTag.data;
                byte[] binMakerNote = binMakerNoteObj.Cast<byte>().ToArray();
                ms.Write(binMakerNote, 10, binMakerNote.Length-10);
                ms.Position = 0; //reset the stream after populate

                headerms.Write(binMakerNote, 0, 10);
                headerms.Position = 0;
                makerNote = new NikonMakerNote(new BinaryReaderBE(ms), new BinaryReaderBE(headerms),0, true);  

                //Get image data
                previewData = this.getfromfile();
                //get Preview Data
                rawData = this.getfromfile();
                //get Raw Data

                
            }
            finally
            {
                fileStream.Close();
            }
            
            //parse to RawImage
            Tag[] makernoteTag = makerNote.parseToStandardExifTag();
            Exif exifTemp = new Exif(makernoteTag);
            RawImage rawImage = new RawImage(exifTemp,rawData,previewData,path);
            //get the imagedata

            rawImage.setImageData(new Image(
                ));

            //get the preview data ( faster than rezising )
            rawImage.setImagePreviewData(new Image());
            return rawImage;
        }
        public Image getfromfile(int x, int y, int offset, int bitdept, bool raw, )
        {
            return null;
        }
    }
}
