using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Effect
{
    static public class Rotate
    {
        public static ushort[] rotate(ref ushort[] image, uint height, uint width, int rotation)
        {
            rotation = rotation % 4;
            ushort[] newImage = new ushort[height * width];
            //for now just left rotate once
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {

                }
            }
            return newImage;
        }
    }
}
