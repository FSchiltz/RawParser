using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RawNet.Format.TIFF
{
    internal class IFD
    {
        public ushort tagNumber;
        public Dictionary<TagType, Tag> tags = new Dictionary<TagType, Tag>();
        public List<IFD> subIFD = new List<IFD>();
        public uint NextOffset { get; protected set; }
        public Endianness endian = Endianness.Unknown;
        public int Depth { private set; get; }
        public int RelativeOffset { protected set; get; }
        public uint Offset { protected set; get; }
        public IFDType type;

        protected static char[] fuji_signature = {
          'F', 'U', 'J', 'I', 'F', 'I', 'L', 'M', (char)0x0c,(char) 0x00,(char) 0x00,(char) 0x00
        };

        protected static char[] nikon_v3_signature = {
          'N', 'i', 'k', 'o', 'n', (char)0x0,(char) 0x2
        };

        private static readonly int MaxRecursion = 20;

        public IFD(Endianness endian, int depth)
        {
            this.endian = endian;
            Depth = depth + 1;
        }

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth) : this(IFDType.Plain, fileStream, offset, endian, depth, 0) { }
        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth, int relativeOffset) : this(IFDType.Plain, fileStream, offset, endian, depth, relativeOffset) { }
        public IFD(IFDType type, TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth) : this(type, fileStream, offset, endian, depth, 0) { }

        public IFD(IFDType type, TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth, int relativeOffset) : this(endian, depth)
        {
            this.type = type;

            if (relativeOffset > 0)
                fileStream.Position = offset + relativeOffset;
            else
                fileStream.Position = offset;

            Offset = offset;
            RelativeOffset = relativeOffset;
            if (depth < IFD.MaxRecursion)
            {
                Parse(fileStream);
                NextOffset = fileStream.ReadUInt32();
            }
        }

        protected void Parse(TIFFBinaryReader fileStream)
        {
            tagNumber = fileStream.ReadUInt16();
            if (tagNumber <= Int16.MaxValue)
            {
                for (int i = 0; i < tagNumber; i++)
                {
                    long tagPos = fileStream.BaseStream.Position;
                    Tag temp = new Tag(fileStream, RelativeOffset);

                    //Special tag
                    switch (temp.TagId)
                    {
                        case TagType.DNGPRIVATEDATA:
                            try
                            {
                                IFD maker_ifd = ParseDngPrivateData(temp);
                                if (maker_ifd != null)
                                {
                                    subIFD.Add(maker_ifd);
                                    temp.data = null;
                                }
                            }
                            catch (RawDecoderException)
                            { // Unparsable private data are added as entries

                            }
                            catch (IOException)
                            { // Unparsable private data are added as entries

                            }

                            break;
                        case TagType.MAKERNOTE:
                        case TagType.MAKERNOTE_ALT:
                            try
                            {
                                var t = fileStream.BaseStream.Position;
                                Makernote makernote = ParseMakerNote(fileStream, temp, endian);
                                if (makernote != null) subIFD.Add(makernote);
                                fileStream.BaseStream.Position = t;
                            }
                            catch (RawDecoderException)
                            { // Unparsable makernotes are added as entries

                            }
                            catch (IOException)
                            { // Unparsable makernotes are added as entries

                            }
                            break;
                        case TagType.FUJI_RAW_IFD:
                        case TagType.NIKONTHUMB:
                        case TagType.SUBIFDS:
                        case TagType.EXIFIFDPOINTER:
                            long p = fileStream.Position;
                            try
                            {
                                for (Int32 k = 0; k < temp.dataCount; k++)
                                {
                                    subIFD.Add(new IFD(IFDType.Plain, fileStream, temp.GetUInt(k), endian, Depth, RelativeOffset));
                                }
                            }
                            catch (RawDecoderException)
                            { // Unparsable subifds are added as entries

                            }
                            catch (IOException)
                            { // Unparsable subifds are added as entries

                            }
                            fileStream.BaseStream.Position = p;
                            break;
                        case TagType.GPSINFOIFDPOINTER:

                            long pos = fileStream.Position;
                            try
                            {
                                subIFD.Add(new IFD(IFDType.GPS, fileStream, temp.GetUInt(0), endian, Depth));
                            }
                            catch (Exception) { }

                            fileStream.BaseStream.Position = pos;
                            break;
                    }

                    if (!tags.ContainsKey(temp.TagId))
                    {
                        tags.Add(temp.TagId, temp);
                    }
                }
            }
        }

        /* This will attempt to parse makernotes and return it as an IFD */
        //makernote should be self contained
        Makernote ParseMakerNote(TIFFBinaryReader reader, Tag tag, Endianness parentEndian)
        {
            //read twice the makernote lenght, should be enough
            reader.BaseStream.Position = tag.dataOffset + RelativeOffset;
            byte[] data = reader.ReadBytes((int)Math.Min(tag.dataCount * 3, reader.BaseStream.Length));
            return ParseMakerNote(data, parentEndian, (int)tag.dataOffset);
        }

        Makernote ParseMakerNote(byte[] data, Endianness parentEndian, int parentOffset)
        {
            if (Depth + 1 > IFD.MaxRecursion) return null;
            uint offset = 0;

            //for nikon makernote read more because they are not always self contained (d100 makernote is not visible as a nikon makernote)


            // Pentax makernote starts with AOC\0 - If it's there, skip it
            if (data[0] == 0x41 && data[1] == 0x4f && data[2] == 0x43 && data[3] == 0)
            {
                return new PentaxMakernote(data, 4, parentOffset, parentEndian, Depth);
                //data = data.Skip(4).ToArray();
                //offset += 4;
            }

            // Pentax also has "PENTAX" at the start, makernote starts at 8
            if (data[0] == 0x50 && data[1] == 0x45
                && data[2] == 0x4e && data[3] == 0x54 && data[4] == 0x41 && data[5] == 0x58)
            {
                return new PentaxMakernote(data, 8, parentOffset, parentEndian, Depth);
                /*
                mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
                parent_end = getTiffEndianness(data.Skip(8).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new RawDecoderException("Cannot determine Pentax makernote endianness");
                //data = data.Skip(10).ToArray();
                offset += 10;*/

            }
            else if (Common.Memcmp(fuji_signature, data))
            {
                return new FujiMakerNote(data, parentEndian, Depth);
                //offset = 12;
                //mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
            }
            else if (Common.Memcmp(nikon_v3_signature, data))
            {
                return new NikonMakerNote(data, Depth);
            }

            // Panasonic has the word Exif at byte 6, a complete Tiff header starts at byte 12
            // This TIFF is 0 offset based
            if (data[6 + offset] == 0x45 && data[7 + offset] == 0x78 && data[8 + offset] == 0x69 && data[9 + offset] == 0x66)
            {
                return new PanasonicMakernote(data.Skip(12).ToArray(), parentEndian, Depth);
                /*
                parent_end = getTiffEndianness(data.Skip(12).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new RawDecoderException("Cannot determine Panasonic makernote endianness");
                offset = 20;*/
            }

            // Some have MM or II to indicate endianness - read that
            if (data[offset] == 0x49 && data[1 + offset] == 0x49)
            {
                offset += 2;
                parentEndian = Endianness.Little;
            }
            else if (data[offset] == 0x4D && data[offset + 1] == 0x4D)
            {
                parentEndian = Endianness.Big;
                offset += 2;
            }

            // Olympus starts the makernote with their own name, sometimes truncated
            if (Common.Strncmp(data, "OLYMP", 5))
            {
                offset += 8;
                if (Common.Strncmp(data, "OLYMPUS", 7))
                {
                    offset += 4;
                }
            }

            // Epson starts the makernote with its own name
            if (Common.Strncmp(data, "EPSON", 5))
            {
                offset += 8;
            }

            // Attempt to parse the rest as an IFD
            try
            {
                return new Makernote(data, offset, parentEndian, Depth, parentOffset);
            }
            catch (Exception e)
            {
                return null;
            }
            // If the structure cannot be read, a RawDecoderException will be thrown.            
        }

        Makernote ParseDngPrivateData(Tag t)
        {
            /*
            1. Six bytes containing the zero-terminated string "Adobe". (The DNG specification calls for the DNGPrivateData tag to start with an ASCII string identifying the creator/format).
            2. 4 bytes: an ASCII string ("MakN" for a Makernote),  indicating what sort of data is being stored here. Note that this is not zero-terminated.
            3. A four-byte count (number of data bytes following); this is the length of the original MakerNote data. (This is always in "most significant byte first" format).
            4. 2 bytes: the byte-order indicator from the original file (the usual 'MM'/4D4D or 'II'/4949).
            5. 4 bytes: the original file offset for the MakerNote tag data (stored according to the byte order given above).
            6. The contents of the MakerNote tag. This is a simple byte-for-byte copy, with no modification.
            */
            uint size = t.dataCount;
            Common.ConvertArray(t.data, out byte[] data);
            Common.ByteToChar(data, out char[] dataAsChar, (int)size);
            string id = new String(dataAsChar);
            /*
            if (id.StartsWith("Microsoft") || id.StartsWith("Nokia")) {
                return new NokiaMakernote(data,0,endian,Depth,0);
                //windows phone dng
            }*/
            if (!id.StartsWith("Adobe"))
            {
                return null;
            }

            if (!(data[6] == 'M' && data[7] == 'a' && data[8] == 'k' && data[9] == 'N'))
            {
                return null;
            }

            data = data.Skip(10).ToArray();
            uint count;

            count = (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3];

            data = data.Skip(4).ToArray();
            if (count > size)
            {
                return null;
            }
            Endianness makernote_endian = Endianness.Unknown;
            if (data[0] == 0x49 && data[1] == 0x49)
                makernote_endian = Endianness.Little;
            else if (data[0] == 0x4D && data[1] == 0x4D)
                makernote_endian = Endianness.Big;
            else
            {
                return null;
            }
            uint org_offset;
            org_offset = (uint)data[2] << 24 | (uint)data[3] << 16 | (uint)data[4] << 8 | data[5];

            data = data.Skip(6).ToArray();
            /* We don't parse original makernotes that are placed after 300MB mark in the original file */
            if (org_offset + count > 300 * 1024 * 1024)
            {
                return null;
            }
            Makernote makerIfd;
            try
            {
                makerIfd = ParseMakerNote(data, makernote_endian, 0);
            }
            catch (RawDecoderException)
            {
                //Makernote are optional and sometimes not even IFD (See Nokia)
                return null;
            }
            return makerIfd;
        }

        public Tag GetEntry(TagType type)
        {
            tags.TryGetValue(type, out Tag tag);
            return tag;
        }

        public List<IFD> GetIFDsWithTag(TagType tag)
        {
            List<IFD> matchingIFDs = new List<IFD>();
            if (tags.ContainsKey(tag))
            {
                matchingIFDs.Add(this);
            }

            foreach (IFD i in subIFD)
            {
                List<IFD> t = (i).GetIFDsWithTag(tag);
                for (int j = 0; j < t.Count; j++)
                {
                    matchingIFDs.Add(t[j]);
                }
            }
            return matchingIFDs;
        }

        public IFD GetIFDWithType(IFDType t)
        {
            if (type == t) return this;
            foreach (IFD i in subIFD)
            {
                var l = i.GetIFDWithType(t);
                if (l != null) { return l; }
            }
            return null;
        }

        protected void MergeIFD(IFD other_tiff)
        {
            if (other_tiff?.subIFD.Count == 0)
                return;

            foreach (IFD i in other_tiff.subIFD)
            {
                subIFD.Add(i);
            }

            foreach (KeyValuePair<TagType, Tag> i in other_tiff.tags)
            {
                tags.Add(i.Key, i.Value); ;
            }
        }

        public Tag GetEntryRecursive(TagType t)
        {
            Tag tag = null;
            tag = GetEntry(t);
            if (tag == null)
            {
                foreach (IFD ifd in subIFD)
                {
                    tag = ifd.GetEntryRecursive(t);
                    if (tag != null) break;
                }
            }
            return tag;
        }
    }
}
