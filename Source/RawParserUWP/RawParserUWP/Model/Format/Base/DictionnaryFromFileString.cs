
namespace RawParser.Model.Parser
{
    class DictionnaryFromFileString: DictionnaryFromFile<string>
    {
        public DictionnaryFromFileString(string file):base(file)
        {
        }

        override public void AddTocontent(ushort key, string contentAsString)
        {
            Add(key, contentAsString);
        }
    }
}
