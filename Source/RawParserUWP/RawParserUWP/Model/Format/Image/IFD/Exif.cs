using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format
{
    class Exif : IFD
    {
        public Exif(Tag[] tags): base(tags)
        {

        }
    }
}
