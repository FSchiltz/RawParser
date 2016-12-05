using RawNet;
using System.Threading.Tasks;
using System;

namespace RawEditor.Effect
{
    enum demosAlgorithm
    {
        NearNeighbour,
        Bicubic,
        Spline,
        Bilinear,
        Deflate
    }

    class Demosaic
    {
        public static void demos(ref RawImage image, demosAlgorithm algo)
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
                    Demosaic.NearNeighbour(image);
                    break;
                case demosAlgorithm.Deflate:
                    Demosaic.Deflate(image);
                    break;
            }
        }

        private static void Deflate(RawImage image)
        {
            //TODO check if correct
            ushort[] deflated = new ushort[image.dim.x * image.dim.y * 3];
            image.cpp = 3;
            Parallel.For(0, image.dim.y, row =>
            {
                int realRow = row + image.mOffset.y;
                for (int col = 0; col < image.dim.x; col++)
                {
                    int realCol = col + image.mOffset.x;
                    int pixeltype = (int)image.cfa.cfa[((row % 2) * 2) + col % 2];

                    deflated[(((row * image.dim.x) + col) * 3) + pixeltype] = image[realRow, realCol];
                }
            });
            image.rawData = deflated;
        }

        private static void NearNeighbour(RawImage image)
        {
            ushort[] deflated = new ushort[image.dim.x * image.dim.y * 3];
            image.cpp = 3;
            Parallel.For(0, image.dim.y, row =>
              {
                  int realRow = row + image.mOffset.y;
                  for (int col = 0; col < image.dim.x; col++)
                  {
                      int realCol = col + image.mOffset.x;
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
