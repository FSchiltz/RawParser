using MathNet.Numerics;
using MathNet.Numerics.Interpolation;

namespace RawEditor.Effect
{
    public static class Curve
    {
        /*
         * Not working correcty
         */
        public static double[] SimpleInterpol(double[] x, double[] y)
        {
            double[] curve = new double[(int)x[x.Length - 1]];
            var f = Fit.PolynomialFunc(x, y, 3);
            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = f(i);
            }
            return curve;
        }

        public static double[] CubicSpline(double[] x, double[] y)
        {
            double[] curve = new double[(int)x[x.Length - 1]];
            var spline = MathNet.Numerics.Interpolation.CubicSpline.InterpolateNaturalInplace(x, y);

            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = spline.Interpolate(i);
            }

            return curve;
        }
    }
}
