using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawNet
{
    public enum ThumbnailType {
        JPEG,
        RAW
    }
    public class Thumbnail
    {
        public byte[] data;
        public iPoint2D dim;
        public ThumbnailType type;
    }
}
