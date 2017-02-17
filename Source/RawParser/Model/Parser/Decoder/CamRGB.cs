namespace RawNet.Decoder
{
    internal class CamRGB
    {
        public string name;
        public short black = 0, white = 0;
        public double[,] matrix; //XYZ to cam matrice (need transformation)
    }
}
