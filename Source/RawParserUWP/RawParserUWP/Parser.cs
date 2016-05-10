using RawParser.Model.ImageDisplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Parser
{
    interface Parser
    {
        RawImage parse(string path);
    }
}
