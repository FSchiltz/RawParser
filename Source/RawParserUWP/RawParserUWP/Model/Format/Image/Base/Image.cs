using RawParser.Model.Format.Base;
using System.IO;
using Windows.UI.Xaml.Media.Imaging;

namespace RawParser.Model.Format
{
    class Image
    {
        public byte[] data { get; set; }
        public int x { set; get; }
        public int y { set; get; }
        private int deep;
        private bool compressed;
        private bool demos;

        public Image()
        {
            compressed = false;
            demos = true;
        }

        public Image ( int x, int y, int deep, bool compressed, bool demos, WriteableBitmap data)
        {
            this.x = x;
            this.y = y;
            this.deep = deep;
            this.compressed = compressed;
            this.demos = demos;
            //this.data = data;
        }

        public Image(double x, double y, uint size, bool raw,BinaryReader reader,uint offset)
        {
            //this.x = x;
            //this.y = y;
            demos = !raw;
            long tempoffset = reader.BaseStream.Position;
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            
            data = new byte[size];
            reader.Read(data,0, (int)size);
        }
 
    }
}
