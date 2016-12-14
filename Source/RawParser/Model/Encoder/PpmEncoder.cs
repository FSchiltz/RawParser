using System.IO;
using System.Text;

namespace RawEditor.Model.Encoder
{
    static class PpmEncoder
    {
        public static void WriteToFile(Stream str, ref ushort[] image, int height, int width, int colorDepth)
        {
            var stream = new StreamWriter(str, Encoding.ASCII);
            stream.Write("P3\r\n" + width + " " + height + " 255 \r\n");
            int shift = colorDepth - 8;
            for (int i = 0; i < height; i++)
            {
                string temp = "";//optimize disk access
                for (int j = 0; j < width; j++)
                {
                    ushort x = image[(int)(((i * width) + j) * 3)];
                    temp += (byte)(x >> shift) + " ";
                    x = image[(int)(((i * width) + j) * 3) + 1];
                    temp += (byte)(x >> shift) + " ";
                    x = image[(int)(((i * width) + j) * 3) + 2];
                    temp += (byte)(x >> shift) + " ";
                }
                temp += "\r\n";
                stream.Write(temp);
            }
            str.Dispose();
        }
    }
}
