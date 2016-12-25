using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSpeed
{
    class TiffIFD
    {
        void TIFF_DEPTH(uint _depth)
        {
            depth = _depth + 1;
            if ((depth) > 10)
            {
                TiffParserException.ThrowTPE("TIFF: sub-micron matryoshka dolls are ignored");
            }
        }

        List<TiffIFD> mSubIFD;
        Dictionary<TiffTag, TiffEntry> mEntry;
        int getNextIFD() { return nextIFD; }

        Endianness endian = Endianness.little;
        FileMap getFileMap() { return mFile; }
        protected int nextIFD = 0;
        FileMap mFile = null;
        UInt32 depth;


        bool isTiffSameAsHost(ref UInt16[] tifftag)
        {
            Endianness host = Common.getHostEndianness();
            if (tifftag[0] == 0x4949)
                return Endianness.little == host;
            if (tifftag[0] == 0x4d4d)
                return Endianness.big == host;
            TiffParserException.ThrowTPE("Unknown Tiff Byteorder :" + tifftag[0]);
            return false;
        }

        Endianness getTiffEndianness(ref UInt16[] tifftag)
        {
            if (tifftag[0] == 0x4949)
                return Endianness.little;
            if (tifftag[0] == 0x4d4d)
                return Endianness.big;
            return Endianness.unknown;
        }

        TiffIFD()
        {
            TIFF_DEPTH(0);
        }

        TiffIFD(ref FileMap f)
        {
            TIFF_DEPTH(0);
            mFile = f;
        }

        TiffIFD(ref FileMap f, UInt32 offset, UInt32 _depth)
        {
            TIFF_DEPTH(_depth);
            mFile = f;
            ushort[] entries;

            entries = *(ushort*)f.getData(offset, 2);    // Directory entries in this IFD

            for (UInt32 i = 0; i < entries; i++)
            {
                int entry_offset = (int)(offset + 2 + i * 12);

                // If the space for the entry is no longer valid stop reading any more as
                // the file is broken or truncated
                if (!mFile.isValid(entry_offset, 12))
                    break;

                TiffEntry t = null;
                try
                {
                    t = new TiffEntry(f, entry_offset, offset);
                }
                catch (IOException)
                { // Ignore unparsable entry
                    continue;
                }

                switch (t.tag)
                {
                    case DNGPRIVATEDATA:
                        {
                            try
                            {
                                TiffIFD* maker_ifd = parseDngPrivateData(t);
                                mSubIFD.push_back(maker_ifd);
                                delete(t);
                            }
                            catch (TiffParserException)
                            { // Unparsable private data are added as entries
                                mEntry[t.tag] = t;
                            }
                            catch (IOException)
                            { // Unparsable private data are added as entries
                                mEntry[t.tag] = t;
                            }
                        }
                        break;
                    case MAKERNOTE:
                    case MAKERNOTE_ALT:
                        {
                            try
                            {
                                mSubIFD.push_back(parseMakerNote(f, t.getDataOffset(), endian));
                                delete(t);
                            }
                            catch (TiffParserException)
                            { // Unparsable makernotes are added as entries
                                mEntry[t.tag] = t;
                            }
                            catch (IOException)
                            { // Unparsable makernotes are added as entries
                                mEntry[t.tag] = t;
                            }
                        }
                        break;

                    case FUJI_RAW_IFD:
                        if (t.type == 0xd) // FUJI - correct type
                            t.type = TIFF_LONG;
                    case SUBIFDS:
                    case EXIFIFDPOINTER:
                        try
                        {
                            for (UInt32 j = 0; j < t.count; j++)
                            {
                                mSubIFD.push_back(new TiffIFD(f, t.getInt(j), depth));
                            }
                            delete(t);
                        }
                        catch (TiffParserException)
                        { // Unparsable subifds are added as entries
                            mEntry[t.tag] = t;
                        }
                        catch (IOException)
                        { // Unparsable subifds are added as entries
                            mEntry[t.tag] = t;
                        }

                        break;
                    default:
                        mEntry[t.tag] = t;
                }
            }
            nextIFD = *(align1_int*)f.getData(offset + 2 + entries * 12, 4);
        }

        TiffIFD parseDngPrivateData(TiffEntry t)
        {
            /*
            1. Six bytes containing the zero-terminated string "Adobe". (The DNG specification calls for the DNGPrivateData tag to start with an ASCII string identifying the creator/format).
            2. 4 bytes: an ASCII string ("MakN" for a Makernote),  indicating what sort of data is being stored here. Note that this is not zero-terminated.
            3. A four-byte count (number of data bytes following); this is the length of the original MakerNote data. (This is always in "most significant byte first" format).
            4. 2 bytes: the byte-order indicator from the original file (the usual 'MM'/4D4D or 'II'/4949).
            5. 4 bytes: the original file offset for the MakerNote tag data (stored according to the byte order given above).
            6. The contents of the MakerNote tag. This is a simple byte-for-byte copy, with no modification.
            */
            UInt32 size = t.count;
            byte[] data = t.getData();
            string id((char*) data);
            if (0 != id.compare("Adobe"))
                TiffParsingException.ThrowTPE("Not Adobe Private data");

            data += 6;
            if (!(data[0] == 'M' && data[1] == 'a' && data[2] == 'k' && data[3] == 'N'))
                ThrowTPE("Not Makernote");

            data += 4;
            UInt32 count;
            if (Endianness.big == Common.getHostEndianness())
                count = *(UInt32*)data;
            else
                count = (unsigned int)data[0] << 24 | (unsigned int)data[1] << 16 | (unsigned int)data[2] << 8 | (unsigned int)data[3];

            data += 4;
            if (count > size)
                ThrowTPE("Error reading TIFF structure (invalid size). File Corrupt");

            Endianness makernote_endian = unknown;
            if (data[0] == 0x49 && data[1] == 0x49)
                makernote_endian = little;
            else if (data[0] == 0x4D && data[1] == 0x4D)
                makernote_endian = big;
            else
                ThrowTPE("Cannot determine endianess of DNG makernote");

            data += 2;
            UInt32 org_offset;

            if (big == getHostEndianness())
                org_offset = *(UInt32*)data;
            else
                org_offset = (unsigned int)data[0] << 24 | (unsigned int)data[1] << 16 | (unsigned int)data[2] << 8 | (unsigned int)data[3];

            data += 4;
            /* We don't parse original makernotes that are placed after 300MB mark in the original file */
            if (org_offset + count > 300 * 1024 * 1024)
                ThrowTPE("Adobe Private data: original offset of makernote is past 300MB offset");

            /* Create fake tiff with original offsets */
            byte[] maker_data = new byte8[org_offset + count];
            memcpy(&maker_data[org_offset], data, count);
            FileMap* maker_map = new FileMap(maker_data, org_offset + count);

            TiffIFD* maker_ifd;
            try
            {
                maker_ifd = parseMakerNote(maker_map, org_offset, makernote_endian);
            }
            catch (TiffParserException &e) {
                delete[] maker_data;
                delete maker_map;
                throw e;
            }
            delete[] maker_data;
            delete maker_map;
            return maker_ifd;
            }

            byte fuji_signature[] = {
  'F', 'U', 'J', 'I', 'F', 'I', 'L', 'M', 0x0c, 0x00, 0x00, 0x00
};

            byte nikon_v3_signature[] = {
  'N', 'i', 'k', 'o', 'n', 0x0, 0x2
};

            /* This will attempt to parse makernotes and return it as an IFD */
            TiffIFD* parseMakerNote(FileMap* f, UInt32 offset, Endianness parent_end)
            {
                FileMap* mFile = f;
                TiffIFD* maker_ifd = null;
                // Get at least 100 bytes which is more than enough for all the checks below
                byte[] data = f.getData(offset, 100);

                // Pentax makernote starts with AOC\0 - If it's there, skip it
                if (data[0] == 0x41 && data[1] == 0x4f && data[2] == 0x43 && data[3] == 0)
                {
                    data += 4;
                    offset += 4;
                }

                // Pentax also has "PENTAX" at the start, makernote starts at 8
                if (data[0] == 0x50 && data[1] == 0x45 && data[2] == 0x4e && data[3] == 0x54 && data[4] == 0x41 && data[5] == 0x58)
                {
                    mFile = new FileMap(f, offset);
                    parent_end = getTiffEndianness((UInt16*)&data[8]);
                    if (parent_end == unknown)
                        ThrowTPE("Cannot determine Pentax makernote endianness");
                    data += 10;
                    offset = 10;
                    // Check for fuji signature in else block so we don't accidentally leak FileMap
                }
                else if (0 == memcmp(fuji_signature, &data[0], sizeof(fuji_signature)))
                {
                    mFile = new FileMap(f, offset);
                    offset = 12;
                }
                else if (0 == memcmp(nikon_v3_signature, &data[0], sizeof(nikon_v3_signature)))
                {
                    offset += 10;
                    mFile = new FileMap(f, offset);
                    data += 10;
                    offset = 8;
                    // Read endianness
                    if (data[0] == 0x49 && data[1] == 0x49)
                    {
                        parent_end = little;
                    }
                    else if (data[0] == 0x4D && data[1] == 0x4D)
                    {
                        parent_end = big;
                    }
                    data += 2;
                }

                // Panasonic has the word Exif at byte 6, a complete Tiff header starts at byte 12
                // This TIFF is 0 offset based
                if (data[6] == 0x45 && data[7] == 0x78 && data[8] == 0x69 && data[9] == 0x66)
                {
                    parent_end = getTiffEndianness((UInt16*)&data[12]);
                    if (parent_end == unknown)
                        ThrowTPE("Cannot determine Panasonic makernote endianness");
                    data += 20;
                    offset += 20;
                }

                // Some have MM or II to indicate endianness - read that
                if (data[0] == 0x49 && data[1] == 0x49)
                {
                    offset += 2;
                    parent_end = little;
                }
                else if (data[0] == 0x4D && data[1] == 0x4D)
                {
                    parent_end = big;
                    offset += 2;
                }

                // Olympus starts the makernote with their own name, sometimes truncated
                if (!strncmp((char*)data, "OLYMP", 5))
                {
                    offset += 8;
                    if (!strncmp((char*)data, "OLYMPUS", 7))
                    {
                        offset += 4;
                    }
                }

                // Epson starts the makernote with its own name
                if (!strncmp((char*)data, "EPSON", 5))
                {
                    offset += 8;
                }

                // Attempt to parse the rest as an IFD
                try
                {
                    if (parent_end == getHostEndianness())
                        maker_ifd = new TiffIFD(mFile, offset, depth);
                    else
                        maker_ifd = new TiffIFDBE(mFile, offset, depth);
                }
                catch (...) {
                if (mFile != f)
                    delete mFile;
                throw;
            }

            if (mFile != f)
                delete mFile;
            // If the structure cannot be read, a TiffParserException will be thrown.
            mFile = f;
            return maker_ifd;
        }

        ~TiffIFD(void) {
            for (map<TiffTag, TiffEntry*>::iterator i = mEntry.begin(); i != mEntry.end(); ++i)
            {
                delete((* i).second);
    }
    mEntry.clear();
            for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
            {
                delete(*i);
}
mSubIFD.clear();
        }

        bool hasEntryRecursive(TiffTag tag)
{
    if (mEntry.find(tag) != mEntry.end())
        return true;
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        if ((*i).hasEntryRecursive(tag))
            return true;
    }
    return false;
}

vector<TiffIFD*> getIFDsWithTag(TiffTag tag)
{
    vector<TiffIFD*> matchingIFDs;
    if (mEntry.find(tag) != mEntry.end())
    {
        matchingIFDs.push_back(this);
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        vector<TiffIFD*> t = (*i).getIFDsWithTag(tag);
        for (UInt32 j = 0; j < t.size(); j++)
        {
            matchingIFDs.push_back(t[j]);
        }
    }
    return matchingIFDs;
}

vector<TiffIFD*> getIFDsWithTagWhere(TiffTag tag, UInt32 isValue)
{
    vector<TiffIFD*> matchingIFDs;
    if (mEntry.find(tag) != mEntry.end())
    {
        TiffEntry* entry = mEntry[tag];
        if (entry.isInt() && entry.getInt() == isValue)
            matchingIFDs.push_back(this);
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        vector<TiffIFD*> t = (*i).getIFDsWithTag(tag);
        for (UInt32 j = 0; j < t.size(); j++)
        {
            matchingIFDs.push_back(t[j]);
        }
    }
    return matchingIFDs;
}

vector<TiffIFD*> getIFDsWithTagWhere(TiffTag tag, string isValue)
{
    vector<TiffIFD*> matchingIFDs;
    if (mEntry.find(tag) != mEntry.end())
    {
        TiffEntry* entry = mEntry[tag];
        if (entry.isString() && 0 == entry.getString().compare(isValue))
            matchingIFDs.push_back(this);
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        vector<TiffIFD*> t = (*i).getIFDsWithTag(tag);
        for (UInt32 j = 0; j < t.size(); j++)
        {
            matchingIFDs.push_back(t[j]);
        }
    }
    return matchingIFDs;
}

TiffEntry* getEntryRecursive(TiffTag tag)
{
    if (mEntry.find(tag) != mEntry.end())
    {
        return mEntry[tag];
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        TiffEntry* entry = (*i).getEntryRecursive(tag);
        if (entry)
            return entry;
    }
    return null;
}

TiffEntry* getEntryRecursiveWhere(TiffTag tag, UInt32 isValue)
{
    if (mEntry.find(tag) != mEntry.end())
    {
        TiffEntry* entry = mEntry[tag];
        if (entry.isInt() && entry.getInt() == isValue)
            return entry;
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        TiffEntry* entry = (*i).getEntryRecursive(tag);
        if (entry)
            return entry;
    }
    return null;
}

TiffEntry* getEntryRecursiveWhere(TiffTag tag, string isValue)
{
    if (mEntry.find(tag) != mEntry.end())
    {
        TiffEntry* entry = mEntry[tag];
        if (entry.isString() && 0 == entry.getString().compare(isValue))
            return entry;
    }
    for (vector<TiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i)
    {
        TiffEntry* entry = (*i).getEntryRecursive(tag);
        if (entry)
            return entry;
    }
    return null;
}

TiffEntry* getEntry(TiffTag tag)
{
    if (mEntry.find(tag) != mEntry.end())
    {
        return mEntry[tag];
    }
    ThrowTPE("TiffIFD: TIFF Parser entry 0x%x not found.", tag);
    return 0;
}


bool hasEntry(TiffTag tag)
{
    return mEntry.find(tag) != mEntry.end();
}

    } // namespace RawSpeed
