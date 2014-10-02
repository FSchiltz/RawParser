using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Parser
{
    class Rational
    {
        private uint a { set; get; }
        private uint b { set; get; }

        public Rational() { }
        public Rational(uint a, uint b)
        {
            this.a = a;
            this.b = b;
        }

    }
}
