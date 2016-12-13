using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RawNet
{
    internal class IFD
    {
        public ushort tagNumber;
        public Dictionary<TagType, Tag> tags = new Dictionary<TagType, Tag>();
        public List<IFD> subIFD = new List<IFD>();
        public uint nextOffset { get; set; }
        public Endianness endian = Endianness.unknown;
        public int depth, relativeOffset;

        char[] fuji_signature = {
  'F', 'U', 'J', 'I', 'F', 'I', 'L', 'M', (char)0x0c,(char) 0x00,(char) 0x00,(char) 0x00
};

        char[] nikon_v3_signature = {
  'N', 'i', 'k', 'o', 'n', (char)0x0,(char) 0x2
};

        Endianness getTiffEndianness(byte[] tifftag)
        {
            if (tifftag[0] == 0x49 && tifftag[1] == 0x49)
                return Endianness.little;
            if (tifftag[0] == 0x4d && tifftag[1] == 0x4d)
                return Endianness.big;
            return Endianness.unknown;
        }

        public IFD() { }

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian) : this(fileStream, offset, endian, 0, 0) { }

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth) : this(fileStream, offset, endian, depth, 0) { }

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth, int relativeOffset)
        {
            this.endian = endian;
            fileStream.Position = offset;
            this.depth = depth;
            this.relativeOffset = relativeOffset;

            Parse(fileStream);
            nextOffset = fileStream.ReadUInt32();
        }

        internal bool hasEntry(TagType t)
        {
            return tags.ContainsKey(t);
        }

        protected void Parse(TIFFBinaryReader fileStream)
        {
            tagNumber = fileStream.ReadUInt16();

            for (int i = 0; i < tagNumber; i++)
            {
                long tagPos = fileStream.BaseStream.Position;
                Tag temp = new Tag(fileStream, relativeOffset);

                //Special tag
                switch (temp.TagId)
                {
                    case TagType.DNGPRIVATEDATA:
                        {
                            try
                            {
                                IFD maker_ifd = parseDngPrivateData(temp);
                                if (maker_ifd != null)
                                {
                                    subIFD.Add(maker_ifd);
                                    temp.data = null;
                                }
                            }
                            catch (TiffParserException)
                            { // Unparsable private data are added as entries

                            }
                            catch (IOException)
                            { // Unparsable private data are added as entries

                            }
                        }
                        break;
                    case TagType.MAKERNOTE:
                    case TagType.MAKERNOTE_ALT:
                        {
                            try
                            {
                                Common.ConvertArray(ref temp.data, out byte[] dest);
                                Makernote makernote = parseMakerNote(dest, endian, temp.dataOffset);
                                if (makernote != null) subIFD.Add(makernote);
                            }
                            catch (TiffParserException)
                            { // Unparsable makernotes are added as entries

                            }
                            catch (IOException)
                            { // Unparsable makernotes are added as entries

                            }
                        }
                        break;

                    case TagType.FUJI_RAW_IFD:
                        if (temp.dataType == TiffDataType.OFFSET) // FUJI - correct type
                            temp.dataType = TiffDataType.LONG;
                        goto case TagType.SUBIFDS;
                    case TagType.SUBIFDS:
                    case TagType.EXIFIFDPOINTER:
                    case TagType.NIKONTHUMB:
                        long p = fileStream.Position;
                        try
                        {
                            for (Int32 k = 0; k < temp.dataCount; k++)
                            {
                                subIFD.Add(new IFD(fileStream, Convert.ToUInt32(temp.data[k]), endian, depth));
                            }
                        }
                        catch (TiffParserException)
                        { // Unparsable subifds are added as entries

                        }
                        catch (IOException)
                        { // Unparsable subifds are added as entries

                        }
                        fileStream.BaseStream.Position = p;
                        break;
                }

                if (!tags.ContainsKey(temp.TagId))
                {
                    tags.Add(temp.TagId, temp);
                }
                else
                {
                    Debug.WriteLine("tags already exist");
                }
            }
        }

        /* This will attempt to parse makernotes and return it as an IFD */
        //makernote should be selfcontained
        Makernote parseMakerNote(byte[] data, Endianness parent_end, uint parentOffset)
        {
            uint offset = 0;
            //reader.Position = off;
            //byte[] data = reader.ReadBytes(100);

            // Pentax makernote starts with AOC\0 - If it's there, skip it
            if (data[0] == 0x41 && data[1] == 0x4f && data[2] == 0x43 && data[3] == 0)
            {
                data = data.Skip(4).ToArray();
                //offset += 4;
            }

            // Pentax also has "PENTAX" at the start, makernote starts at 8
            if (data[0] == 0x50 && data[1] == 0x45
                && data[2] == 0x4e && data[3] == 0x54 && data[4] == 0x41 && data[5] == 0x58)
            {
                return new PentaxMakernote(data.Skip(8).ToArray());
                /*
                mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
                parent_end = getTiffEndianness(data.Skip(8).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new TiffParserException("Cannot determine Pentax makernote endianness");
                //data = data.Skip(10).ToArray();
                offset += 10;*/

            }
            else if (Common.memcmp(ref fuji_signature, ref data))
            {
                return new FujiMakerNote(data);
                //offset = 12;
                //mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
            }
            else if (Common.memcmp(ref nikon_v3_signature, ref data))
            {
                return new NikonMakerNote(data);
            }

            // Panasonic has the word Exif at byte 6, a complete Tiff header starts at byte 12
            // This TIFF is 0 offset based
            if (data[6 + offset] == 0x45 && data[7 + offset] == 0x78 && data[8 + offset] == 0x69 && data[9 + offset] == 0x66)
            {
                return new PanasonicMakernote(data.Skip(12).ToArray());
                /*
                parent_end = getTiffEndianness(data.Skip(12).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new TiffParserException("Cannot determine Panasonic makernote endianness");
                offset = 20;*/
            }

            // Some have MM or II to indicate endianness - read that
            if (data[offset] == 0x49 && data[1 + offset] == 0x49)
            {
                offset += 2;
                parent_end = Endianness.little;
            }
            else if (data[offset] == 0x4D && data[offset + 1] == 0x4D)
            {
                parent_end = Endianness.big;
                offset += 2;
            }

            // Olympus starts the makernote with their own name, sometimes truncated
            if (Common.strncmp(data, "OLYMP", 5))
            {
                offset += 8;
                if (Common.strncmp(data, "OLYMPUS", 7))
                {
                    offset += 4;
                }
            }

            // Epson starts the makernote with its own name
            if (Common.strncmp(data, "EPSON", 5))
            {
                offset += 8;
            }

            // Attempt to parse the rest as an IFD
            try
            {
                /* if (parent_end == getHostEndianness())
                     maker_ifd = new IFD(mFile, offset, depth);
                 else
                     maker_ifd = new IFDBE(mFile, offset, depth);*/
                return new Makernote(data, offset, parent_end, depth, parentOffset);

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
            // If the structure cannot be read, a TiffParserException will be thrown.            
        }

        Makernote parseDngPrivateData(Tag t)
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
            Common.ConvertArray(ref t.data, out byte[] data);
            Common.ByteToChar(ref data, out char[] dataAsChar);
            string id = new String(dataAsChar);
            if (!id.StartsWith("Adobe"))
            {
                Debug.WriteLine("Not Adobe Private data");
                return null;
            }

            if (!(data[6] == 'M' && data[7] == 'a' && data[8] == 'k' && data[9] == 'N'))
            {
                Debug.WriteLine("Not Makernote");
                return null;
            }

            data = data.Skip(10).ToArray();
            uint count;

            count = (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3];

            data = data.Skip(4).ToArray();
            if (count > size)
            {
                Debug.WriteLine("Error reading TIFF structure (invalid size). File Corrupt");
                return null;
            }
            Endianness makernote_endian = Endianness.unknown;
            if (data[0] == 0x49 && data[1] == 0x49)
                makernote_endian = Endianness.little;
            else if (data[0] == 0x4D && data[1] == 0x4D)
                makernote_endian = Endianness.big;
            else
            {
                Debug.WriteLine("Cannot determine endianess of DNG makernote");
                return null;
            }
            data = data.Skip(2).ToArray();
            uint org_offset;


            org_offset = (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3];

            data = data.Skip(4).ToArray();
            /* We don't parse original makernotes that are placed after 300MB mark in the original file */
            if (org_offset + count > 300 * 1024 * 1024)
            {
                Debug.WriteLine("Adobe Private data: original offset of makernote is past 300MB offset");
                return null;
            }
            /* Create fake tiff with original offsets */
            //byte[] maker_data = new byte[count];
            // Common.memcopy(ref maker_data, ref data, count, 0, 0);            
            Makernote maker_ifd;
            try
            {
                maker_ifd = parseMakerNote(data, makernote_endian, 0);
            }
            catch (TiffParserException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
            return maker_ifd;
        }

        public Tag getEntry(TagType type)
        {
            tags.TryGetValue(type, out Tag tag);
            return tag;
        }

        public List<IFD> getIFDsWithTag(TagType tag)
        {
            List<IFD> matchingIFDs = new List<IFD>();
            if (tags.ContainsKey(tag))
            {
                matchingIFDs.Add(this);
            }

            foreach (IFD i in subIFD)
            {
                List<IFD> t = (i).getIFDsWithTag(tag);
                for (int j = 0; j < t.Count; j++)
                {
                    matchingIFDs.Add(t[j]);
                }
            }
            return matchingIFDs;
        }

        public Tag getEntryRecursive(TagType t)
        {
            Tag tag = null;
            tag = getEntry(t);
            if (tag == null)
            {
                foreach (IFD ifd in subIFD)
                {
                    tag = ifd.getEntryRecursive(t);
                    if (tag != null) break;
                }
            }
            return tag;
        }
    }
}
