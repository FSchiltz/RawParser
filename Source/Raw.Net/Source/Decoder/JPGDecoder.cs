using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace RawNet
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    /*
     * This will decode all image supportedby the windows parser 
     * Should be a last resort parser
     */
    internal class JPGParser : RawDecoder
    {
        IRandomAccessStream stream;

        public JPGParser(TIFFBinaryReader file, CameraMetaData meta) : base(ref file, meta)
        {

        }

        protected override void checkSupportInternal()
        {
            stream = file.BaseStream.AsRandomAccessStream();

        }

        protected override void decodeMetaDataInternal()
        {
            //fill useless metadata
            mRaw.metadata.wbCoeffs = new float[] { 1, 1, 1, 1 };
        }

        protected override RawImage decodeRawInternal()
        {
            mRaw.colorDepth = 8;
            mRaw.cpp = 3;
            mRaw.bpp = 8;
            var decoder = BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, file.BaseStream.AsRandomAccessStream()).AsTask();

            decoder.Wait();

            var bitmapasync = decoder.Result.GetSoftwareBitmapAsync().AsTask();
            bitmapasync.Wait();
            var image = bitmapasync.Result;
            using (BitmapBuffer buffer = image.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    mRaw.dim = new Point2D(bufferLayout.Width, bufferLayout.Height);
                    mRaw.Init();
                    unsafe
                    {
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);

                        for (int y = 0; y < mRaw.dim.y; y++)
                        {
                            int realY = y * mRaw.dim.x * 3;
                            int bufferY = y * mRaw.dim.x * 4 + +bufferLayout.StartIndex;
                            for (int x = 0; x < mRaw.dim.x; x++)
                            {
                                int realPix = realY + (3 * x);
                                int bufferPix = bufferY + (4 * x);
                                mRaw.rawData[realPix] = temp[bufferPix +2];
                                mRaw.rawData[realPix + 1] = temp[bufferPix + 1];
                                mRaw.rawData[realPix + 2] = temp[bufferPix];
                            }

                        }
                    }
                }
            }
            return mRaw;
        }
    }
}