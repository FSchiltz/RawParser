using RawParser.Model.ImageDisplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RawParser.Model.Parser
{
    class DNGParser : TIFFParser
    {
        protected class DNGHeader : Header
        {
            
        }

        protected class DNGIFD : IFD
        {
          
        }
        override public RawImage parse(string path)
        {
            DNGHeader header = new DNGHeader();
            DNGIFD ifd = new DNGIFD();
            BinaryReader fileStream = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
            try
            {
                //read the header
                header.byteOrder = fileStream.ReadUInt16();

                if (header.byteOrder == 0x4D4D)
                {
                    //File is in reverse bit order
                    fileStream = new BinaryReaderBE(new FileStream(path, FileMode.Open, FileAccess.Read), System.Text.Encoding.BigEndianUnicode);
                    fileStream.ReadUInt16();
                }
                
                header.TIFFMagic = fileStream.ReadUInt16();
                if (header.TIFFMagic != 42) throw new Exception();

                header.TIFFoffset = fileStream.ReadUInt16();

                //read the IFD
                base.readIFD(fileStream, header.TIFFoffset, ifd, false);
                


            }
            finally
            {
                fileStream.Close();
            }

            string tempstr = " ";
            for (int i = 0; i < ifd.tagNumber; i++)
            {
                tempstr += "[" + ifd.tags[i].tagId + ":";
                for (int j = 0; j < ifd.tags[i].dataCount; j++)
                {
                    tempstr += ifd.tags[i].data[j] + ":";
                }
                tempstr += "]";
            }
            MessageBox.Show(
                header.byteOrder
                + " " + header.TIFFMagic
                + " " + header.TIFFoffset +
                tempstr, "Result");

            // Pixel [][] pixelBuffer = new Pixel ()[][];
            //RawImage rawImage = new RawImage(new Exif(), new Dimension(), pixelBuffer);
            //return rawImage;
            return null;           
        }
    }
}
