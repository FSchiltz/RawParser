using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format.Base
{
    class Rational
    {
        uint a;
        uint b;

        public Rational(uint a, uint b)
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
