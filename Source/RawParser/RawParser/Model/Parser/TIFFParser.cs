using RawParser.Model.ImageDisplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Parser
{
    class TIFFParser : Parser
    {
        protected class Header
        {
            public ushort byteOrder;
            public ushort TIFFMagic;
            public uint TIFFoffset;
        }

        protected struct Tag
        {
            public ushort tagId;
            public ushort dataType;
            public uint dataCount;
            public uint dataOffset;
            public Object[] data;
        }

        protected class IFD
        {
            public ushort tagNumber;
            public Tag[] tags;

            public ushort nextOffset { get; set; }

            //Optimize
            public Tag findTag(uint a)
            {
                int i = 0;
                while (i < tags.Length && tags[i].tagId != a)
                { i++; }
                if (i >= tags.Length) throw new Exception();
                return tags[i];
            }
        }

        protected class MarkerNote
        {

        }

        protected void readMarkerNote(BinaryReader filestream, uint offset, MarkerNote note)
        {

        }

        protected void readIFD(BinaryReader fileStream, uint offset, IFD ifd, bool compression)
        {
            fileStream.BaseStream.Seek(offset, SeekOrigin.Begin);
            ifd.tagNumber = fileStream.ReadUInt16();
            ifd.tags = new Tag[ifd.tagNumber];
            for (int i = 0; i < ifd.tagNumber; i++)
            {
                Tag temp = new Tag();
                temp.tagId = fileStream.ReadUInt16();
                temp.dataType = fileStream.ReadUInt16();
                temp.dataCount = fileStream.ReadUInt32();
                //Todo improve condition
                temp.dataOffset = 0;
                if (!compression && (temp.dataCount > 2 && temp.dataType == 3 )||(temp.dataCount > 1 && temp.dataType == 4)|| (temp.dataCount > 4 && (temp.dataType == 1 || temp.dataType == 2)) || ( temp.dataType == 5) ) 
                {
                    temp.dataOffset = fileStream.ReadUInt32();
                }
                temp.data = new Object[temp.dataCount];
                long firstPosition = fileStream.BaseStream.Position;
                if (temp.dataOffset > 1)
                {
                    fileStream.BaseStream.Seek(temp.dataOffset,SeekOrigin.Begin);
                }
                
                for (int j = 0; j < temp.dataCount; j++)
                {
                    switch (temp.dataType)
                    {
                        case 1: temp.data[j] = fileStream.ReadSByte();
                            break;
                        case 2:                        
                            string str = "";
                            char chartemp;
                            do
                            {
                                chartemp = fileStream.ReadChar();
                                str += chartemp;
                            } while (chartemp != '\0');

                            temp.data[j] = str;
                            break;
                        case 3: temp.data[j] = fileStream.ReadUInt16();
                            if(temp.dataOffset == 0 && temp.dataCount == 1)fileStream.ReadUInt16();
                            break;
                        case 4: temp.data[j] = fileStream.ReadUInt32();
                            break;
                        case 5: temp.data[j] = new Rational(fileStream.ReadUInt32(), fileStream.ReadUInt32());
                            break;
                        case 6:
                            break;
                        case 7:
                            break;
                        case 8:
                            break;
                        case 9:
                            break;
                        case 10:
                            break;
                        case 11:
                            break;
                        case 12:
                            break;
                    }
                }
                if (temp.dataOffset > 1)
                {
                    fileStream.BaseStream.Seek(firstPosition, SeekOrigin.Begin);
                }
                ifd.tags[i] = temp;
            }
            ifd.nextOffset = fileStream.ReadUInt16();
        }

        virtual public RawImage parse(string path) { return null;}
    }
}
