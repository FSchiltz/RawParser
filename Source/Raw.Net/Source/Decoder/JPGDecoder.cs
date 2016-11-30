using System;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RawNet
{
    public class JPGParser : RawDecoder
    {
        public JPGParser(ref TIFFBinaryReader file) : base(ref file)
        {

        }

        protected override void checkSupportInternal(CameraMetaData meta)
        {
            throw new NotImplementedException();
        }

        protected override void decodeMetaDataInternal(CameraMetaData meta)
        {
            throw new NotImplementedException();
        }

        protected override RawImage decodeRawInternal()
        {
            throw new NotImplementedException();
        }
    }
}