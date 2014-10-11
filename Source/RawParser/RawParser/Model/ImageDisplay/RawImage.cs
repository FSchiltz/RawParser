using RawParser.Model.Format;
using RawParser.Model.Format.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.ImageDisplay
{

    class RawImage
    {
        private string fileName {get;set;}
        private Exif exif;
        private Pixel[][] imageData;
        protected Pixel[][] imagePreviewData;
        public RawImage(Exif e ,Pixel[][] d, Pixel[][] p, string name)
        {
            exif = e;
            imageData = d;
            imagePreviewData = p;
            fileName = name;            
        }
    }
}
