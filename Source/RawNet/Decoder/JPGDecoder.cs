using PhotoNet.Common;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace RawNet.Decoder
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
    internal class JPGDecoder : RawDecoder
    {
        //BitmapPropertiesView meta;

        public JPGDecoder(Stream file) : base(file) { }

        public override void DecodeMetadata()
        {
            //fill useless metadata
            rawImage.IsGammaCorrected = false;
            rawImage.whitePoint = byte.MaxValue;
            rawImage.metadata.RawDim = new Point2D(rawImage.fullSize.UncroppedDim.width, rawImage.fullSize.UncroppedDim.height);
            rawImage.metadata.WbCoeffs = new WhiteBalance(1, 1, 1);
            /*List<string> list = new List<string>
            {
                "/app1/ifd/{ushort=271}"
            };
            var metaList = meta.GetPropertiesAsync(list);
            metaList.AsTask().Wait();
            if (metaList.GetResults() != null)
            {
                metaList.GetResults().TryGetValue("/app1/ifd/{ushort=271}", out var make);
                rawImage.metadata.make = make?.Value.ToString();
            }*/
        }

        public override void DecodeRaw()
        {
            rawImage.fullSize.ColorDepth = 8;
            rawImage.fullSize.cpp = 3;
            rawImage.isCFA = false;
            var decoder = BitmapDecoder.CreateAsync(stream.AsRandomAccessStream()).AsTask();
            decoder.Wait();
            var bitmapasync = decoder.Result.GetSoftwareBitmapAsync().AsTask();
            // meta = decoder.Result.BitmapProperties;
            bitmapasync.Wait();
            var image = bitmapasync.Result;
            using (BitmapBuffer buffer = image.LockBuffer(BitmapBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                rawImage.fullSize.dim = new Point2D((uint)bufferLayout.Width, (uint)bufferLayout.Height);
                rawImage.Init(true);
                unsafe
                {
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                    for (int y = 0; y < rawImage.fullSize.dim.height; y++)
                    {
                        long bufferY = y * rawImage.fullSize.dim.width * 4 + bufferLayout.StartIndex;
                        for (int x = 0; x < rawImage.fullSize.dim.width; x++)
                        {
                            long bufferPix = bufferY + (4 * x);
                            long position = (y * rawImage.fullSize.dim.width) + x;
                            rawImage.fullSize.red[position] = temp[bufferPix + 2];
                            rawImage.fullSize.green[position] = temp[bufferPix + 1];
                            rawImage.fullSize.blue[position] = temp[bufferPix];
                        }

                    }
                }
            }
        }
    }
}