using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.ImageDisplay
{
    struct Pixel
    {
        ushort R;
        ushort G;
        ushort B;
        ushort A;
    }

    struct Exif
    {
        //replace by optimised key value 
    }
    
    struct Dimension
    {
        int h;
        int l;
    }

    class RawImage
    {
        private string fileName {get;set;}
        private Exif exif;
        private Dimension dimension;
        private Pixel[][] imageData;
        public RawImage(Exif e, Dimension d, Pixel[][] p) { 
        }
    }
}
