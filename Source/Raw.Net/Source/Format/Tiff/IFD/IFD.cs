using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RawNet
{
    public class IFD
    {
        public ushort tagNumber;
        public Dictionary<ushort, Tag> tags;
        public List<IFD> subIFD = new List<IFD>();
        public ushort nextOffset { get; set; }
        public Endianness endian;
        public int depth;

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

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian)
        {
            this.endian = endian;
            fileStream.Position = offset;
            tagNumber = fileStream.ReadUInt16();
            tags = new Dictionary<ushort, Tag>();

            for (int i = 0; i < tagNumber; i++)
            {
                Tag temp = new Tag();
                temp.tagId = (TagType)fileStream.ReadUInt16();
                //add the displayname 
                temp.displayName = null;

                temp.dataType = (TiffDataType)fileStream.ReadUInt16();
                temp.dataCount = fileStream.ReadUInt32();

                //IF makernote, do not parse data
                //if (temp.tagId == TagType.MAKERNOTE || temp.tagId == TagType.MAKERNOTE_ALT) temp.dataCount = 0;

                temp.dataOffset = 0;
                if (((temp.dataCount * temp.getTypeSize(temp.dataType) > 4)))
                {
                    temp.dataOffset = fileStream.ReadUInt32();
                }

                //Get the tag data
                temp.data = new Object[temp.dataCount];
                long firstPosition = fileStream.Position;
                if (temp.dataOffset > 1)
                {
                    fileStream.Position = temp.dataOffset;
                    //todo check if correct
                }

                if (temp.tagId != TagType.MAKERNOTE && temp.tagId != TagType.MAKERNOTE_ALT)
                {
                    for (int j = 0; j < temp.dataCount; j++)
                    {
                        switch (temp.dataType)
                        {
                            case TiffDataType.BYTE:
                            case TiffDataType.UNDEFINED:
                            case TiffDataType.ASCII:
                            case TiffDataType.OFFSET:
                                temp.data[j] = fileStream.ReadByte();
                                break;
                            case TiffDataType.SHORT:
                                temp.data[j] = fileStream.ReadUInt16();
                                break;
                            case TiffDataType.LONG:
                                temp.data[j] = fileStream.ReadUInt32();
                                break;
                            case TiffDataType.RATIONAL:
                                temp.data[j] = fileStream.ReadDouble();
                                break;
                            case TiffDataType.SBYTE:
                                temp.data[j] = fileStream.ReadSByte();
                                break;
                            case TiffDataType.SSHORT:
                                temp.data[j] = fileStream.ReadInt16();
                                //if (temp.dataOffset == 0 && temp.dataCount == 1) fileStream.ReadInt16();
                                break;
                            case TiffDataType.SLONG:
                                temp.data[j] = fileStream.ReadInt32();
                                break;
                            case TiffDataType.SRATIONAL:
                                //Because the nikonmakernote is broken with the tag 0x19 wich is double but offset of zero.
                                //TODO remove this Fix
                                if (temp.dataOffset == 0)
                                {
                                    temp.data[j] = .0;
                                }
                                else
                                {
                                    temp.data[j] = fileStream.ReadDouble();
                                }
                                break;
                            case TiffDataType.FLOAT:
                                temp.data[j] = fileStream.ReadSingle();
                                break;
                            case TiffDataType.DOUBLE:
                                temp.data[j] = fileStream.ReadDouble();
                                break;
                        }
                    }

                }//Special tag
                switch (temp.tagId)
                {
                    case TagType.DNGPRIVATEDATA:
                        {
                            try
                            {
                                IFD maker_ifd = parseDngPrivateData(temp);
                                subIFD.Add(maker_ifd);
                                temp.data = null;
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
                                //save current position
                                long pos = fileStream.Position;

                                subIFD.Add(parseMakerNote(fileStream, temp.dataOffset, endian));

                                //correct here
                                fileStream.BaseStream.Position = pos;
                                //return to current position
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
                //transform data ToString
                if (temp.dataType == TiffDataType.ASCII)
                {
                    //remove \0 if any
                    if ((byte)temp.data[temp.dataCount - 1] == 0) temp.data[temp.dataCount - 1] = (byte)' ';
                    string t = Encoding.ASCII.GetString(temp.data.Cast<byte>().ToArray());
                    temp.data = new Object[1];
                    temp.data[0] = t;
                }

                if (temp.dataOffset > 1)
                {
                    fileStream.BaseStream.Position = firstPosition;
                }
                else if (temp.dataOffset == 0)
                {
                    int k = (int)temp.dataCount * temp.getTypeSize(temp.dataType);
                    if (k < 4)
                        fileStream.ReadBytes(4 - k);
                }

                /*else
                {
                    temp.dataCount = 0;
                    temp.data = null;
                }*/
                if (!tags.ContainsKey((ushort)temp.tagId))
                {
                    tags.Add((ushort)temp.tagId, temp);
                }
                else
                {
                    Debug.WriteLine("tags already exist");
                }
            }
            nextOffset = fileStream.ReadUInt16();
        }

        public IFD(TIFFBinaryReader fileStream, uint offset, Endianness endian, int depth) :
                this(fileStream, offset, endian)
        {

            this.depth = depth;
        }

        /* This will attempt to parse makernotes and return it as an IFD */
        IFD parseMakerNote(TIFFBinaryReader reader, uint off, Endianness parent_end)
        {
            IFD maker_ifd = null;
            uint offset = 0;
            TIFFBinaryReader mFile = null;
            reader.Position = off;
            byte[] data = reader.ReadBytes(100);
            // Pentax makernote starts with AOC\0 - If it's there, skip it
            if (data[0] == 0x41 && data[1] == 0x4f && data[2] == 0x43 && data[3] == 0)
            {
                //data = data.Skip(4).ToArray();
                offset += 4;
            }

            // Pentax also has "PENTAX" at the start, makernote starts at 8
            if (data[0 + offset] == 0x50 && data[1 + offset] == 0x45
                && data[2 + offset] == 0x4e && data[3 + offset] == 0x54 && data[4 + offset] == 0x41 && data[5 + offset] == 0x58)
            {
                mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
                parent_end = getTiffEndianness(data.Skip(8).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new TiffParserException("Cannot determine Pentax makernote endianness");
                //data = data.Skip(10).ToArray();
                offset += 10;
                // Check for fuji signature in else block so we don't accidentally leak FileMap
            }
            else if (Common.memcmp(ref fuji_signature, ref data))
            {
                offset = 12;
                mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
            }
            else if (Common.memcmp(ref nikon_v3_signature, ref data))
            {
                //offset = 10;
                offset = 10;
                // Read endianness
                if (data[0 + offset] == 0x49 && data[1 + offset] == 0x49)
                {
                    parent_end = Endianness.little;
                    mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
                    offset = 8;
                }
                else if (data[0 + offset] == 0x4D && data[1 + offset] == 0x4D)
                {
                    parent_end = Endianness.big;
                    mFile = new TIFFBinaryReaderRE(reader.BaseStream, offset + off, (uint)data.Length);
                    offset = 8;
                }
            }

            // Panasonic has the word Exif at byte 6, a complete Tiff header starts at byte 12
            // This TIFF is 0 offset based
            if (data[6] == 0x45 && data[7] == 0x78 && data[8] == 0x69 && data[9] == 0x66)
            {
                parent_end = getTiffEndianness(data.Skip(12).ToArray());
                if (parent_end == Endianness.unknown)
                    throw new TiffParserException("Cannot determine Panasonic makernote endianness");
                offset = 20;
            }

            // Some have MM or II to indicate endianness - read that
            if (data[0] == 0x49 && data[1] == 0x49)
            {
                offset += 2;
                parent_end = Endianness.little;
            }
            else if (data[0] == 0x4D && data[1] == 0x4D)
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
                if (mFile == null)
                {
                    if (parent_end == Endianness.little)
                    {
                        mFile = new TIFFBinaryReader(reader.BaseStream, offset + off, (uint)data.Length);
                    }
                    else if (parent_end == Endianness.big)
                    {
                        mFile = new TIFFBinaryReaderRE(reader.BaseStream, offset + off, (uint)data.Length);
                    }
                }
                /* if (parent_end == getHostEndianness())
                     maker_ifd = new IFD(mFile, offset, depth);
                 else
                     maker_ifd = new IFDBE(mFile, offset, depth);*/
                maker_ifd = new IFD(mFile, offset, endian, depth);

            }
            catch (Exception e)
            {
                if (!(e is TiffParserException)) throw new TiffParserException(e.Message);
            }
            // If the structure cannot be read, a TiffParserException will be thrown.

            return maker_ifd;
        }

        IFD parseDngPrivateData(Tag t)
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
                throw new TiffParserException("Not Adobe Private data");

            if (!(data[6] == 'M' && data[7] == 'a' && data[8] == 'k' && data[9] == 'N'))
                throw new TiffParserException("Not Makernote");

            data = data.Skip(10).ToArray();
            uint count;

            count = (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3];

            data = data.Skip(4).ToArray();
            if (count > size)
                throw new TiffParserException("Error reading TIFF structure (invalid size). File Corrupt");

            Endianness makernote_endian = Endianness.unknown;
            if (data[0] == 0x49 && data[1] == 0x49)
                makernote_endian = Endianness.little;
            else if (data[0] == 0x4D && data[1] == 0x4D)
                makernote_endian = Endianness.big;
            else
                throw new TiffParserException("Cannot determine endianess of DNG makernote");

            data = data.Skip(2).ToArray();
            uint org_offset;


            org_offset = (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3];

            data = data.Skip(4).ToArray();
            /* We don't parse original makernotes that are placed after 300MB mark in the original file */
            if (org_offset + count > 300 * 1024 * 1024)
                throw new TiffParserException("Adobe Private data: original offset of makernote is past 300MB offset");

            /* Create fake tiff with original offsets */
            //byte[] maker_data = new byte[count];
            // Common.memcopy(ref maker_data, ref data, count, 0, 0);
            TIFFBinaryReader maker_map = new TIFFBinaryReader(TIFFBinaryReader.streamFromArray(data));

            IFD maker_ifd;
            try
            {
                maker_ifd = parseMakerNote(maker_map, 0, makernote_endian);
            }
            catch (TiffParserException e)
            {
                throw e;
            }
            return maker_ifd;
        }


        public Tag getEntry(TagType type)
        {
            tags.TryGetValue((ushort)type, out Tag tag);
            return tag;
        }

        public List<IFD> getIFDsWithTag(TagType tag)
        {
            List<IFD> matchingIFDs = new List<IFD>();
            if (tags.ContainsKey((ushort)tag))
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

        internal bool hasEntry(TagType t)
        {
            foreach (var tag in tags)
            {
                if (tag.Value.tagId == t) return true;
            }
            return false;
        }

        internal bool hasEntryRecursive(TagType t)
        {
            if (hasEntry(t)) return true;
            foreach (IFD ifd in subIFD)
            {
                if (ifd.hasEntryRecursive(t))
                    return true;
            }
            return false;
        }

        internal Tag getEntryRecursive(TagType t)
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
