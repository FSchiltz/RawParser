using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format.Base
{
    class SRational
    {
        int a;
        int b;

        public SRational(int a, int b)
        {
            this.a = a;
            this.b = b;
        }

        public double get()
        {
            return a / b;
        }
    }
}
