using System;

namespace RawNet
{
    public sealed class GPSInfo
    {
        public double[] longitude;
        public double[] lattitude;
        public long altitudeRef;
        public double altitude;
        public string lattitudeRef;
        public string longitudeRef;
        public string LattitudeAsString => lattitude[0] + "°" + lattitude[1] + "'" + lattitude[2] + "\"" + lattitudeRef;
        public string LongitudeAsString => longitude[0] + "°" + longitude[1] + "'" + longitude[2] + "\"" + longitudeRef;
        public string AltitudeAsString => Math.Sign(altitudeRef) * altitude + "";
    }

    public enum ColorSpaceType
    {
        sRGB = 1,
        AdobeRGB = 2,
        WideGamutRGB = 0xfffd,
        ICCProfile = 0xfffe,
        Uncalibrated = 0xffff
    }

    public class ImageMetadata
    {
        public string FileName { get; set; }
        public string FileNameComplete { get; set; }
        public long ParsingTime { get; set; }
        public string FileExtension { get; set; }
        public string ParsingTimeAsString => ParsingTime / 1000 + "s " + ParsingTime % 1000 + "ms";

        // Aspect ratio of the pixels, usually 1 but some cameras need scaling
        // <1 means the image needs to be stretched vertically, (0.5 means 2x)
        // >1 means the image needs to be stretched horizontally (2 mean 2x)
        public double PixelAspectRatio { get; set; }

        // White balance coefficients of the image
        public float[] WbCoeffs { get; set; } = new float[4];

        // How many pixels far down the left edge and far up the right edge the image 
        // corners are when the image is rotated 45 degrees in Fuji rotated sensors.
        public uint FujiRotationPos { get; set; }

        public Point2D Subsampling { get; set; } = new Point2D();
        public string Make { get; set; }
        public string Model { get; set; }
        public string Mode { get; set; }
        public string Copyright { get; set; }

        public int Rotation { get; set; }
        public int IsoSpeed { get; set; }
        public double Exposure { get; set; }
        public double Aperture { get; set; }
        public double Focal { get; set; }
        public string Lens { get; set; }

        public string TimeTake { get; set; }
        public string TimeModify { get; set; }

        public GPSInfo Gps { get; set; }
        public Point2D RawDim { get; set; }
        public int OriginalRotation { get; set; }

        public string Comment { get; set; }
        public ColorSpaceType ColorSpace { get; set; } = ColorSpaceType.Uncalibrated;

        public ImageMetadata()
        {
            Subsampling.width = Subsampling.height = 1;
            IsoSpeed = 0;
            PixelAspectRatio = 1;
            FujiRotationPos = 0;
            WbCoeffs[0] = WbCoeffs[1] = WbCoeffs[2] = WbCoeffs[3] = 1;
        }

        public string ExposureAsString => (Exposure >= 1) ? Exposure + "s" : "1/" + (1 / Exposure).ToString("F0") + "s";
    }
}
