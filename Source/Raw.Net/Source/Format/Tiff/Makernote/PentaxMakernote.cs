using System.Diagnostics;

namespace RawNet
{
    internal class PentaxMakernote : Makernote
    {
        public PentaxMakernote(byte[] data)
        {
            TIFFBinaryReader buffer;
            if (data[0] == 0x4D && data[1] == 0x4D)
            {
                buffer = new TIFFBinaryReaderRE(data);
                //TODO see if need to move
            }
            else if (data[0] == 0x49 && data[1] == 0x49)
            {
                buffer = new TIFFBinaryReaderRE(data);
            }
            else
            {
                throw new RawDecoderException("Makernote endianess unknown " + data[0]);
            }
            buffer.BaseStream.Position += 2;
            buffer.ReadUInt16();
            uint TIFFoffset = buffer.ReadUInt32();
            buffer.BaseStream.Position = TIFFoffset;
            //offset are from the start of the tag
            tagNumber = buffer.ReadUInt16();

            for (int i = 0; i < tagNumber; i++)
            {
                long tagPos = buffer.BaseStream.Position;
                Tag temp = new Tag(buffer, (int)tagPos + 6);
                if (!tags.ContainsKey(temp.TagId))
                {
                    tags.Add(temp.TagId, temp);
                }
                else
                {
                    Debug.WriteLine("tags already exist");
                }
            }
            buffer.Dispose();
        }
    }
}