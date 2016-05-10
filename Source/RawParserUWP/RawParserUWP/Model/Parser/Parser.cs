using RawParser.Model.ImageDisplay;
using System.IO;

namespace RawParser.Model.Parser
{
    interface Parser
    {
        RawImage parse(Stream file);
    }
}
