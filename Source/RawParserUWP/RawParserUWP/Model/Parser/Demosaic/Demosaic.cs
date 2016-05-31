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
        public static BitArray demos(BitArray bitArray, ulong height, ulong width, ushort colorDepth , demosAlgorithm algo)
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
                    return Demosaic.NearNeighbour(bitArray, height, width, colorDepth);
                    break;


            }
            throw new NotImplementedException();
        }

        private static BitArray NearNeighbour(BitArray bitArray, ulong height, ulong width, ushort colorDepth)
        {
            return bitArray;
        }
    }
}
