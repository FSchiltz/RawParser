using System.IO;
using System.Text;

namespace RawParser.Model.Encoder
{
    class PpmEncoder
    {
        public static void WriteToFile(Stream str, ref ushort [] image, uint height, uint width, int colorDepth)
        {
    
            var stream = new StreamWriter(str, Encoding.ASCII);
            stream.Write("P3\r\n" + width + " " + height + " 255 \r\n");
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    ushort x = image[(int)(((i * width) + j) * 3)];
                    byte y = (byte)(x >> 6);
                    stream.Write(y + " ");
                    x = image[(int)(((i * width) + j) * 3) + 1];
                    y = (byte)(x >> 6);
                    stream.Write(y + " ");
                    x = image[(int)(((i * width) + j) * 3) + 2];
                    y = (byte)(x >> 6);
                    stream.Write(y + " ");
                }
                stream.Write("\r\n");
            }
            str.Dispose();
        }
    }
}
