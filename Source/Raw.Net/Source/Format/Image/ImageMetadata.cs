using System;

namespace RawNet
{
    public class ImageMetaData
    {
        public string fileName { get; set; }
        public string fileNameComplete { get; set; }
        // Aspect ratio of the pixels, usually 1 but some cameras need scaling
        // <1 means the image needs to be stretched vertically, (0.5 means 2x)
        // >1 means the image needs to be stretched horizontally (2 mean 2x)
        public double pixelAspectRatio;

        // White balance coefficients of the image
        public float[] wbCoeffs = new float[4];

        // How many pixels far down the left edge and far up the right edge the image 
        // corners are when the image is rotated 45 degrees in Fuji rotated sensors.
        public UInt32 fujiRotationPos;

        public Point2D subsampling = new Point2D();
        public string make;
        public string model;
        public string mode;

        public string canonical_make;
        public string canonical_model;
        public string canonical_alias;
        public string canonical_id;

        // ISO speed. If known the value is set, otherwise it will be '0'.
        public int isoSpeed;

        public ImageMetaData()
        {
            subsampling.x = subsampling.y = 1;
            isoSpeed = 0;
            pixelAspectRatio = 1;
            fujiRotationPos = 0;
            wbCoeffs[0] = 1;
            wbCoeffs[1] = 1;
            wbCoeffs[2] = 1;
            wbCoeffs[3] = 1;
        }
    }
}
