namespace RawNet
{
    public class WhiteBalance
    {
        public double Red { get; set; } = 1;
        public double Blue { get; set; } = 1;
        public double Green { get; set; } = 1;

        public WhiteBalance(double red, double green, double blue)
        {
            Red = red;
            Blue = blue;
            Green = green;
        }

        public WhiteBalance(int red, int green, int blue, uint colorDepth)
        {
            Red = red/(double)green;
            Blue = blue/(double)green;
        }
    }
}