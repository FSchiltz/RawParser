
namespace RawNet
{
    class DictionnaryFromFileString: DictionnaryFromFile<string>
    {
        public DictionnaryFromFileString(string file):base(file)
        {
        }

        public override void addTocontent(ushort key, string contentAsString)
        {
            Add(key, contentAsString);
        }
    }
}
