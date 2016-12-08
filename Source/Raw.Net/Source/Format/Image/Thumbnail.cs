using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawNet
{
    public enum ThumbnailType
    {
        JPEG,
        RAW
    }

    public class Thumbnail
    {
        public byte[] data;
        public Point2D dim { get; set; }
        public ThumbnailType type { get; set; }
    }
}
