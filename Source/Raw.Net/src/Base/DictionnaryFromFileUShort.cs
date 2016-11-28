using System;

namespace RawNet
{
    class DictionnaryFromFileUShort : DictionnaryFromFile<ushort>
    {
        public DictionnaryFromFileUShort(string file):base(file)
        {
        }

        override public void addTocontent(ushort key, string contentAsString)
        {
            Add(key, Convert.ToUInt16(contentAsString,16));
        }
    }
}
