using RawParser.Model.Format;
using RawParser.Model.ImageDisplay;
using System.IO;

namespace RawParser.Model.Parser
{
    class DNGParser : Parser
    {
        public RawImage parse(Stream file)
        {
            BinaryReader fileStream = new BinaryReader(file);
                      
            try
            {
                Header header = new Header(fileStream, 0);
                if (header.byteOrder == 0x4D4D)
                {
                    //File is in reverse bit order
                    fileStream = new BinaryReaderBE(file, System.Text.Encoding.BigEndianUnicode);
                    header = new Header(fileStream, 0);
                }
                IFD ifd = new IFD(fileStream, header.TIFFoffset, false);  
            }
            finally
            {
               // fileStream.Close();
            }
                        
            // Pixel [][] pixelBuffer = new Pixel ()[][];
            //RawImage rawImage = new RawImage(new Exif(), new Dimension(), pixelBuffer);
            //return rawImage;
            return null;           
        }
    }
}
