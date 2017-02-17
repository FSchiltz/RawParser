namespace RawNet.Decoder
{
    internal class CamRGB
    {
        public CamRGB(string name, ushort black, ushort white, double[,] matrix)
        {
            this.name = name;
            this.black = black;
            this.white = white;
            this.matrix = matrix;
        }
        public string name;
        public ushort black = 0, white = 0;
        public double[,] matrix; //XYZ to cam matrice (need transformation)
    }
}
