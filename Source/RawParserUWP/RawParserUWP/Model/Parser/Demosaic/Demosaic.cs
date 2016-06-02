using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParserUWP.Model.Parser.Demosaic
{
    enum demosAlgorithm
    {
        NearNeighbour,
        Bicubic,
        Spline,
        Bilinear
    }

    class Demosaic
    {
        public static BitArray demos(BitArray bitArray, int height, int width, ushort colorDepth, demosAlgorithm algo, byte[] cfa)
        {
            switch (algo)
            {

                case demosAlgorithm.Bilinear:
                    break;
                case demosAlgorithm.Bicubic:
                    break;
                case demosAlgorithm.Spline:
                    break;
                case demosAlgorithm.NearNeighbour:
                default:
                    return Demosaic.NearNeighbour(bitArray, height, width, colorDepth, cfa);
                    break;


            }
            throw new NotImplementedException();
        }

        private static BitArray NearNeighbour(BitArray bitArray, int height, int width, ushort colorDepth,byte[] cfa)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var pixeltype = cfa[((row % 2) * 2) + col % 2] * colorDepth;

                    if (pixeltype == 1)
                    {
                        //if green
                        for(int k =0;k < colorDepth; k++)
                        {
                            //get the red (above)
                            if(row > 0)
                                bitArray[(((row * width) + col) * 3 * colorDepth) + k] = bitArray[((((row-1) * width) + col) * 3 * colorDepth) + k];
                            //get the blue (left)
                            if(col > 0)
                                bitArray[(((row * width) + col) * 3 * colorDepth) + (2* colorDepth)+k] = bitArray[(((row * width) + col-1) * 3 * colorDepth) + (2 * colorDepth) + k];
                        }
                    }
                    else
                    {
                        //get the green value from around
                        if (pixeltype == 0)
                        {
                            //if red
                            for (int k = 0; k < colorDepth; k++)
                            {
                                if (col > 0 && row > 0)
                                    bitArray[(((row * width) + col) * 3 * colorDepth) + (2 * colorDepth) + k] = bitArray[((((row -1)* width) + col -1) * 3 * colorDepth) + (2 * colorDepth) + k];
                            }
                        }
                        else if (pixeltype == 2)
                        {
                            //if blue
                            for (int k = 0; k < colorDepth; k++)
                            {
                                //get the red (above)
                                if (row > 0 && col > 0)
                                    bitArray[(((row * width) + col) * 3 * colorDepth) + k] = bitArray[((((row - 1) * width) + col - 1) * 3 * colorDepth) + k];
                            }
                        }
                    }
                }
            }
            return bitArray;
        }
    }
}
