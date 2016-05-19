using RawParser.Model.Format;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Media;

namespace RawParser.Model.ImageDisplay
{

    class RawImage
    {
        private string fileName {get;set;}
        public Dictionary<ushort, Tag> exif;
        public Image imageData { set; get; }
        public Image imagePreviewData { get; set; }

        public RawImage(Dictionary<ushort, Tag> e ,Image d, Image p)
        {
            exif = e;
            imageData = d;
            imagePreviewData = p;           
        }

        internal ImageSource getImageasBitMap()
        {
            throw new NotImplementedException();
        }
    }
}
