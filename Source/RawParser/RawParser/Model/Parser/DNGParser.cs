using RawParser.Model.Format;
using RawParser.Model.Format.Image.IFD;
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
        override public RawImage parse(string path)
        {
            BinaryReader fileStream = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
                      
            try
            {
                Header header = new Header(fileStream, 0);
                if (header.byteOrder == 0x4D4D)
                {
                    //File is in reverse bit order
                    fileStream = new BinaryReaderBE(new FileStream(path, FileMode.Open, FileAccess.Read), System.Text.Encoding.BigEndianUnicode);
                    header = new Header(fileStream, 0);
                }
                DNGIFD ifd = new DNGIFD(fileStream, header.TIFFoffset, false);  
            }
            finally
            {
                fileStream.Close();
            }
                        
            // Pixel [][] pixelBuffer = new Pixel ()[][];
            //RawImage rawImage = new RawImage(new Exif(), new Dimension(), pixelBuffer);
            //return rawImage;
            return null;           
        }
    }
}
