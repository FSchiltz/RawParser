using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

namespace RawParser.Model.Parser
{
    abstract class DictionnaryFromFile<T> : Dictionary<ushort, T>
    {

        internal DictionnaryFromFile(string file)
        {
            ReadFile(file);
        }

        public async void ReadFile(string fileName)
        {
            StorageFolder store = ApplicationData.Current.LocalFolder;
            StorageFile file = null;
            file = await store.GetFileAsync(fileName);
            // Open the file into a streamreader
            using (StreamReader stream = new StreamReader((await file.OpenReadAsync()).AsStreamForRead()))
            {
                while (!stream.EndOfStream) // Keep reading until we get to the end
                {
                    string splitMe = stream.ReadLine();
                    string[] tempString = splitMe.Split(new char[] { ' ' }); //Split at the space

                    if (tempString.Length < 2) // If we get less than 2 results, discard them
                        continue;
                    else if (tempString.Length == 2)
                    { // Easy part. If there are 2 results, add them to the dictionary
                        AddTocontent(Convert.ToUInt16(tempString[0].Trim()), tempString[1].Trim());

                    }
                    else if (tempString.Length > 2)
                    {
                        string temp = "";
                        for (int i = 0; i < tempString.Length - 1; i++)
                        {
                            temp += tempString[i];
                        }
                        AddTocontent(Convert.ToUInt16(tempString[0].Trim()), temp);
                    }
                }
            }
        }

        public abstract void AddTocontent(ushort key, string contentAsString);
    }
}
