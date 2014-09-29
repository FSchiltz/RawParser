using RawParser.Model.ImageDisplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Parser
{
    class Nefparser : Parser
    {
        public RawImage parse(string path)
        {
            byte[] buffer;
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            try
            {
                int length = (int)fileStream.Length;
            }
            finally
            {
                fileStream.Close();
            }
            return null;
        }
    }
}
