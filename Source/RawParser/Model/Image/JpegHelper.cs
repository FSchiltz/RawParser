using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace RawEditor
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    class JpegHelper
    {

        public static SoftwareBitmap getJpegInArray(byte[] im)
        {
            if (im == null) return null;
            Task t;
            IAsyncOperation<BitmapDecoder> decoder;
            IAsyncOperation<SoftwareBitmap> bitmapasync;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(im, 0, im.Length);
                ms.Position = 0; //reset the stream after populate

                decoder = BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());

                t = Task.Run(() =>
                {
                    while (decoder.Status == AsyncStatus.Started)
                    {
                    }
                });
                t.Wait();
                if (decoder.Status == AsyncStatus.Error)
                {

                    throw decoder.ErrorCode;
                }

                bitmapasync = decoder.GetResults().GetSoftwareBitmapAsync();

                t = Task.Run(() =>
                {
                    while (bitmapasync.Status == AsyncStatus.Started)
                    {
                    }
                });
                t.Wait();
                if (bitmapasync.Status == AsyncStatus.Error)
                {
                    throw bitmapasync.ErrorCode;
                }
            }
            return bitmapasync.GetResults();
        }

        public static unsafe SoftwareBitmap getImageAs8bitsBitmap(ref ushort[] data, int height, int width, int colorDepth, object[] curve, ref int[] value, bool histo, bool bgr)
        {
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            using (BitmapBuffer buffer = image.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* tempByteArray;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out tempByteArray, out capacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    //calculte diff between colordepth and 8
                    int diff = (colorDepth) - 8;

                    for (int i = 0; i < bufferLayout.Width * bufferLayout.Height; i++)
                    {
                        //get the pixel                    
                        ushort red = (ushort)(data[(i * 3)] >> diff),
                        green = (ushort)(data[(i * 3) + 1] >> diff),
                        blue = (ushort)(data[(i * 3) + 2] >> (diff));
                        if (blue > 255) blue = 255;
                        if (red > 255) red = 255;
                        if (green > 255) green = 255;
                        if (histo) value[(ushort)((red + green + blue) / 3)]++;
                        if (bgr)
                        {
                            tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)blue;
                            tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)green;
                            tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)red;
                        }
                        else
                        {
                            tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)red;
                            tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)green;
                            tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)blue;
                        }
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 3] = 255;
                    }
                }
            }
            return image;
        }

    }
}
