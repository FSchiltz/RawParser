using RawParser.Model.ImageDisplay;

namespace RawParser.Model.Parser
{
    interface Parser
    {
        RawImage parse(string path);
    }
}
