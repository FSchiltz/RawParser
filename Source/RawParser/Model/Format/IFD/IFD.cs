using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RawParser.Format.IFD
{
    class IFD
    {
        public ushort tagNumber;
        public Dictionary<ushort, Tag> tags;

        public ushort nextOffset { get; set; }

        public IFD(BinaryReader fileStream, uint offset, bool compression, bool relativeOffset)
        {
            fileStream.BaseStream.Position = offset;
            tagNumber = fileStream.ReadUInt16();
            tags = new Dictionary<ushort, Tag>();
            for (int i = 0; i < tagNumber; i++)
            {
                Tag temp = new Tag();
                temp.tagId = fileStream.ReadUInt16();
                //add the displayname 
                temp.displayName = null;

                temp.dataType = fileStream.ReadUInt16();
                temp.dataCount = fileStream.ReadUInt32();

                temp.dataOffset = 0;
                if (!compression || ((temp.dataCount * temp.getTypeSize(temp.dataType) > 4)))
                {
                    temp.dataOffset = fileStream.ReadUInt32();
                }
              

                if (temp.tagId != 0x0096 && temp.tagId != 0x927C)//if makernote or linearisation table, do not get the data ( optimisation)
                {
                    temp.data = new Object[temp.dataCount];
                    long firstPosition = fileStream.BaseStream.Position;
                    if (temp.dataOffset > 1)
                    {
                        var realoffset = temp.dataOffset;
                        if (relativeOffset)
                        {
                            realoffset += offset - 8;
                            //TODO removemagicvalue and fix
                        }

                        fileStream.BaseStream.Seek(realoffset, SeekOrigin.Begin);
                        //todocheck if correct
                    }

                    for (int j = 0; j < temp.dataCount; j++)
                    {
                        switch (temp.dataType)
                        {
                            case 1:
                            case 2:
                            case 7:
                                temp.data[j] = fileStream.ReadByte();
                                break;
                            case 3:
                                temp.data[j] = fileStream.ReadUInt16();
                                break;
                            case 4:
                                temp.data[j] = fileStream.ReadUInt32();
                                break;
                            case 5:
                                temp.data[j] = fileStream.ReadDouble();
                                break;
                            case 6:
                                temp.data[j] = fileStream.ReadSByte();
                                break;
                            case 8:
                                temp.data[j] = fileStream.ReadInt16();
                                //if (temp.dataOffset == 0 && temp.dataCount == 1) fileStream.ReadInt16();
                                break;
                            case 9:
                                temp.data[j] = fileStream.ReadInt32();
                                break;
                            case 10:
                                //Because the nikonmakernote is broken with the tag 0x19 wich is double but offset of zero.
                                if (temp.dataOffset == 0)
                                {
                                    temp.data[j] = .0;
                                }
                                else
                                {
                                    temp.data[j] = fileStream.ReadDouble();
                                }
                                break;
                            case 11:
                                temp.data[j] = fileStream.ReadSingle();
                                break;
                            case 12:
                                temp.data[j] = fileStream.ReadDouble();
                                break;
                        }
                    }

                    //transform data ToString
                    if (temp.dataType == 2)
                    {
                        string t = Encoding.ASCII.GetString(temp.data.Cast<byte>().ToArray());
                        temp.data = new Object[1];
                        temp.data[0] = t;
                    }

                    if (temp.dataOffset > 1)
                    {
                        fileStream.BaseStream.Seek(firstPosition, SeekOrigin.Begin);
                    }
                    else if (temp.dataOffset == 0)
                    {
                        int k = (int)temp.dataCount * temp.getTypeSize(temp.dataType);
                        if (k < 4)
                            fileStream.ReadBytes(4 - k);
                    }
                }else
                {
                    temp.dataCount = 0;
                    temp.data = null;
                }
                if (!tags.ContainsKey(temp.tagId))
                {
                    tags.Add(temp.tagId, temp);
                }
                else
                {
                    Debug.WriteLine("tags already exist");
                }
            }
            nextOffset = fileStream.ReadUInt16();
        }
    }
}
