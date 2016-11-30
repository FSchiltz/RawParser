using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RawNet
{
    /*
     * This will decode all image supportedby the windows parser 
     * Should be a last resort parser
     */
    public class JPGParser : RawDecoder
    {
        WriteableBitmap image;
        bool done = false;
        public JPGParser(TIFFBinaryReader file) : base(ref file)
        {
            //TODO change
            //needs to be in the UI thread, Why???
            var t = new Task(async () =>
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {// UI thread does post update operations
                    image = new WriteableBitmap(1, 1);
                    done = true;
                });
            });
            t.Start();
            t.Wait();
            var x = new Task(() => { while (done) { Task.Delay(200); } });
            x.Start();
            x.Wait();
        }

        protected override void checkSupportInternal(CameraMetaData meta)
        {
            var r = mFile.BaseStream.AsRandomAccessStream();
            done = false;
            //needs to be in the UI thread, Why???
            var t = new Task(async () =>
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {// UI thread does post update operations
                    image.SetSource(r);
                    image.Invalidate();
                    done = true;
                });
            });
            t.Start();
            t.Wait();
            var x = new Task(() => { while (done) { } });
            x.Start();
            x.Wait();
        }

        protected override void decodeMetaDataInternal(CameraMetaData meta)
        {
            //fill useless metadata
            mRaw.metadata.wbCoeffs = new float[] { 1, 1, 1, 1 };
            mRaw.colorDepth = 8;
            mRaw.cpp = 3;
        }

        protected override RawImage decodeRawInternal()
        {
            done = false;
            var t = new Task(async () =>
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {// UI thread does post update operations
                    try
                    {
                        var stream = image.PixelBuffer.AsStream();
                        mRaw.dim = new iPoint2D(image.PixelHeight, image.PixelWidth);
                        ushort[] raw = new ushort[image.PixelHeight * image.PixelWidth * 3];
                        for (int i = 0; i < image.PixelWidth * image.PixelHeight * 3; i++)
                        {
                            raw[i] = (ushort)stream.ReadByte();
                        }
                        done = true;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                });
            });
            t.Start();
            t.Wait();
            var x = new Task(() => { while (done) { Task.Delay(200); } });
            x.Start();
            x.Wait();
            return mRaw;
        }
    }
}