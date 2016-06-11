using System;
using System.Collections.Generic;
using System.IO;
using RawParser.Format.IFD;
using RawParser.Parser;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RawParser.Parser
{
    internal class JPGParser : AParser
    {
        WriteableBitmap image;
        /*
         * Use the built in jpeg reader
         */
        public override void Parse(Stream s)
        {
            image = new WriteableBitmap(1, 1);
            image.SetSource(s.AsRandomAccessStream());
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            return null;
        }

        public override byte[] parsePreview()
        {
            return null;
        }

        public override ushort[] parseRAWImage()
        {
            IBuffer buffer = image.PixelBuffer;
            ushort[] raw = new ushort[image.PixelHeight*image.PixelWidth*3];
            for(int i =0; i < image.PixelWidth * image.PixelHeight * 3;i++)
            {
               // raw[i] = buffer;
            }
            return null;
        }

        public override byte[] parseThumbnail()
        {
            return null;
        }
    }
}