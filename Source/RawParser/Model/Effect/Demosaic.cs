using RawNet;
using System.Threading.Tasks;
using System;

namespace RawEditor
{
    public enum DemosAlgorithm
    {
        Bicubic,
        Spline,
        Bilinear,
        Deflate,
        AHD
    }

    public class Demosaic
    {
        public static void Demos(ref RawImage image, DemosAlgorithm algo)
        {
            ushort[] deflated = new ushort[image.dim.x * image.dim.y * 3];
            image.cpp = 3;
            switch (algo)
            {
                case DemosAlgorithm.AHD:
                    Demosaic.AHD(image, deflated);
                    break;
                //break;
                case DemosAlgorithm.Bicubic:
                    Demosaic.Bicubic(image, deflated);
                    break;
                case DemosAlgorithm.Spline:
                //break;
                case DemosAlgorithm.Bilinear:
                    Demosaic.Bilinear(image, deflated);
                    break;
                case DemosAlgorithm.Deflate:
                    Demosaic.Deflate(image, deflated);
                    break;
            }
            //set correct dim
            image.offset = new Point2D();
            image.rawData = deflated;
        }

        private static void AHD(RawImage image, ushort[] deflated)
        {
            throw new NotImplementedException();
        }

        private static void Bicubic(RawImage image, ushort[] deflated)
        {
            throw new NotImplementedException();
        }

        private static void Deflate(RawImage image, ushort[] deflated)
        {
            Parallel.For(0, image.dim.y, row =>
            {
                int realRow = row + image.offset.y;
                for (int col = 0; col < image.dim.x; col++)
                {
                    int realCol = col + image.offset.x;
                    int pixeltype = (int)image.cfa.cfa[((row % 2) * 2) + col % 2];

                    deflated[(((row * image.dim.x) + col) * 3) + pixeltype] = image[realRow, realCol];
                }
            });
        }

        private static void Bilinear(RawImage image, ushort[] deflated)
        {
            Parallel.For(0, image.dim.y, row =>
              {
                  int realRow = row + image.offset.y;
                  for (int col = 0; col < image.dim.x; col++)
                  {
                      int realCol = col + image.offset.x;
                      CFAColor pixeltype = image.cfa.cfa[((row % 2) * 2) + col % 2];
                      if (pixeltype == CFAColor.GREEN)
                      {
                          //if green
                          //get the green
                          deflated[(((row * image.dim.x) + col) * 3) + 1] = image[realRow, realCol];

                          //get the red(                        
                          deflated[((row * image.dim.x) + col) * 3] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol]) >> 1);
                          //get the blue (left)
                          deflated[(((row * image.dim.x) + col) * 3) + 2] = (ushort)((image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 1);
                      }
                      else
                      {
                          deflated[(((row * image.dim.x) + col) * 3) + (int)pixeltype] = image[realRow, realCol];

                          //get the green value from around
                          pixeltype = (CFAColor)((int)pixeltype ^ 2);
                          deflated[(((row * image.dim.x) + col) * 3) + 1] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol] + image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 2);

                          //get the other value
                          deflated[(((row * image.dim.x) + col) * 3) + (int)pixeltype] = (ushort)((image[realRow - 1, realCol - 1] + image[realRow - 1, realCol + 1] + image[realRow + 1, realCol - 1] + image[realRow + 1, realCol + 1]) >> 2);
                      }
                  }
              });
            image.rawData = deflated;

        }
    }
}
