using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RawParser.Model.Parser
{
    abstract class DictionnaryFromFile<T> : Dictionary<ushort, T>
    {

        internal DictionnaryFromFile(string file)
        {
            ReadFile(file);
        }

        public void ReadFile(string fileName)
        {
            int lineread = 0, linediscarder = 0;
            StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;            
            StorageFile file = null;
            IRandomAccessStream tempvar = null;

            Task t = Task.Run(async () =>
            {
                file = await installationFolder.GetFileAsync(fileName);
            });
            t.Wait();

            t = Task.Run(async () =>
            {
                tempvar = await file.OpenReadAsync();
            });
            t.Wait();


            // Open the file into a streamreader
            using (StreamReader stream = new StreamReader(tempvar.AsStreamForRead()))
            {

                while (!stream.EndOfStream) // Keep reading until we get to the end
                {
                    lineread++;
                    string splitMe = stream.ReadLine();
                    if (splitMe.Trim() != "")
                    {
                        string[] tempString = splitMe.Split(new char[] { ' ' }); //Split at the space

                        if (tempString.Length < 2)
                        { // If we get less than 2 results, discard them
                            linediscarder++;
                        }
                        else
                        {
                            string temp = "";
                            for (int i = 1; i < tempString.Length; i++)
                            {
                                temp += tempString[i];
                            }
                            AddTocontent(Convert.ToUInt16(tempString[0].Trim(), 16), temp);
                        }
                    }
                }
            }
        }

        public abstract void AddTocontent(ushort key, string contentAsString);
    }
}
