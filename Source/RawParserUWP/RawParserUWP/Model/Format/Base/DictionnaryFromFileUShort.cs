
using System;

namespace RawParser.Model.Parser
{
    class DictionnaryFromFileUShort : DictionnaryFromFile<ushort>
    {
        public DictionnaryFromFileUShort(string file):base(file)
        {
        }

        override public void AddTocontent(ushort key, string contentAsString)
        {
            Add(key, Convert.ToUInt16(contentAsString));
        }
    }
}
