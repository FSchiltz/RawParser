using MathNet.Numerics;
using MathNet.Numerics.Interpolation;

namespace RawEditor.Effect
{
    public class Curve
    {
        /*
         * Not working correcty
         */
        public static double[] simpleInterpol(double[] x, double[] y)
        {
            double[] curve = new double[(int)x[x.Length - 1]];
            var f = Fit.PolynomialFunc(x, y, 3);
            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = f(i);
            }
            return curve;
        }

        public static double[] cubicSpline(double[] x, double[] y)
        {
            double[] curve = new double[(int)x[x.Length - 1]];
            var spline = CubicSpline.InterpolateNaturalInplace(x, y);

            for (int i = 0; i < curve.Length; i++)
            {
                curve[i] = spline.Interpolate(i);
            }

            return curve;
        }
    }
}
