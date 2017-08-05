using MathNet.Numerics;
using System.Diagnostics;

namespace PhotoNet
{
    public static class Curve
    {
        /*
         * Not working correcty
         */
        public static double[] SimpleInterpol(double[] xCoordinates, double[] yCoordinates)
        {
            Debug.Assert(xCoordinates.Length >= 2);
            Debug.Assert(yCoordinates.Length >= 2);
            Debug.Assert(xCoordinates.Length == yCoordinates.Length);
            double[] curve = new double[(int)xCoordinates[xCoordinates.Length - 1]];
            var f = Fit.PolynomialFunc(xCoordinates, yCoordinates, 3);
            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = f(i);
            }
            return curve;
        }

        //interpolate normalized value
        public static double[] CubicSpline(double[] xCoordinates, double[] yCoordinates)
        {
            Debug.Assert(xCoordinates.Length >= 2);
            Debug.Assert(yCoordinates.Length >= 2);
            Debug.Assert(xCoordinates.Length == yCoordinates.Length);
            var curve = new double[(int)xCoordinates[xCoordinates.Length - 1] + 1];
            var spline = MathNet.Numerics.Interpolation.CubicSpline.InterpolateNaturalInplace(xCoordinates, yCoordinates);

            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = spline.Interpolate(i);
            }
            return curve;
        }
    }
}
