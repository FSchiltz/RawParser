using RawParser.Model.Format.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format.Image.Base
{
    class Image
    {
        private Pixel[][] data;
        private int x;
        private int y;
        private int deep;
        private bool compressed;
        private bool demos;

        public Image()
        {
            compressed = false;
            demos = true;
        }

        public Image ( int x, int y, int deep, bool compressed, bool demos, Pixel[][] data)
        {
            this.x = x;
            this.y = y;
            this.deep = deep;
            this.compressed = compressed;
            this.demos = demos;
            this.data = data;
        }
    }
}
