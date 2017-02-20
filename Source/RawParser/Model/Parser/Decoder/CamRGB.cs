using System.Diagnostics;

namespace RawNet.Decoder
{
    internal class CamRGB
    {
        public CamRGB(string name, ushort black, ushort white, double[,] matrix) : this(name, black, white)
        {
            Debug.Assert(matrix.Length == 9 || matrix.Length == 12);
            this.matrix = matrix;
        }

        public CamRGB(string name, ushort black, ushort white, double[] matrix) : this(name, black, white)
        {
            Debug.Assert(matrix.Length == 9 || matrix.Length == 12);
            this.matrix = new double[,] { { matrix[0], matrix[1], matrix[2] }, { matrix[3], matrix[4], matrix[5] }, { matrix[6], matrix[7], matrix[8] } };
        }

        public CamRGB(string name, ushort black, ushort white)
        {
            this.name = name;
            this.black = black;
            this.white = white;
        }

        public string name;
        public ushort black = 0, white = 0;
        public double[,] matrix; //XYZ to cam matrice (need transformation)
    }
}
