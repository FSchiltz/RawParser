using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{

    public class TiffParser
    {
        TIFFBinaryReader reader;
        public IFD rootIFD;
        Stream stream;
        public TiffParser(Stream stream)
        {
            this.stream = stream;
        }

        public void parseData()
        {
            if (stream.Length < 16)
                throw new TiffParserException("Not a TIFF file (size too small)");
            Endianness endian = Endianness.little;
            byte[] data = new byte[5];
            stream.Position = 0;
            stream.Read(data, 0, 4);
            if (data[0] == 0x4D || data[1] == 0x4D)
            {
                //open binaryreader
                reader = new TIFFBinaryReaderRE(stream);
                endian = Endianness.big;

                if (data[3] != 42 && data[2] != 0x4f) // ORF sometimes has 0x4f, Lovely!
                    throw new TiffParserException("Not a TIFF file (magic 42)");
            }
            else if (data[0] == 0x49 || data[1] == 0x49)
            {
                reader = new TIFFBinaryReader(stream);
                if (data[2] != 42 && data[2] != 0x52 && data[2] != 0x55) // ORF has 0x52, RW2 0x55 - Brillant!
                    throw new TiffParserException("Not a TIFF file (magic 42)");
            }
            else
            {
                throw new TiffParserException("Not a TIFF file (ID)");
            }

            UInt32 nextIFD;
            reader.Position = 4;
            nextIFD = reader.ReadUInt32();
            rootIFD = new IFD(reader, nextIFD, endian, 0);
            nextIFD = rootIFD.nextOffset;
            while (nextIFD != 0)
            {
                rootIFD.subIFD.Add(new IFD(reader, nextIFD, endian, 0));
                if (rootIFD.subIFD.Count > 100)
                {
                    throw new TiffParserException("TIFF file has too many SubIFDs, probably broken");
                }
                nextIFD = (rootIFD.subIFD[rootIFD.subIFD.Count - 1]).nextOffset;
            }
        }

        public void mergeIFD(TiffParser other_tiff)
        {
            if (other_tiff?.rootIFD?.subIFD.Count == 0)
                return;

            IFD other_root = other_tiff.rootIFD;
            foreach (IFD i in other_root.subIFD)
            {
                rootIFD.subIFD.Add(i);
            }

            foreach (KeyValuePair<TagType, Tag> i in other_root.tags)
            {
                rootIFD.tags.Add(i.Key, i.Value); ;
            }
            other_root.subIFD.Clear();
            other_root.subIFD.Clear();
        }

        public RawDecoder getDecoder()
        {
            if (rootIFD == null)
                parseData();

            List<IFD> potentials = new List<IFD>();
            potentials = rootIFD.getIFDsWithTag(TagType.DNGVERSION);

            /* Copy, so we can pass it on and not have it destroyed with ourselves */
            IFD root = rootIFD;

            if (potentials.Count != 0)
            {  // We have a dng image entry
                IFD t = potentials[0];
                t.tags.TryGetValue(TagType.DNGVERSION, out Tag tag);
                object[] c = tag.data;
                if (Convert.ToInt32(c[0]) > 1)
                    throw new TiffParserException("DNG version too new.");
                rootIFD = null;
                return new DngDecoder(root, ref reader);
            }

            potentials = rootIFD.getIFDsWithTag(TagType.MAKE);

            if (potentials.Count > 0)
            {  // We have make entry
                foreach (IFD i in potentials)
                {
                    i.tags.TryGetValue(TagType.MAKE, out Tag tag);
                    string make = tag.dataAsString;
                    make = make.Trim();
                    //remove trailing \0 if any

                    string model = "";
                    i.tags.TryGetValue(TagType.MODEL, out Tag tagModel);
                    if (tagModel != null)
                    {
                        model = tagModel.dataAsString;
                        model = make.Trim();
                    }
                    switch (make)
                    {
                        /*
                        case "Canon":
                            rootIFD = null;
                            return new Cr2Decoder(root, reader);
                        case "FUJIFILM":
                            rootIFD = null;
                            return new RafDecoder(root, reader);*/
                        case "NIKON CORPORATION":
                        case "NIKON":
                            rootIFD = null;
                            return new NefDecoder(ref root, reader);
                            /*
                        case "OLYMPUS IMAGING CORP.":
                        case "OLYMPUS CORPORATION":
                        case "OLYMPUS OPTICAL CO.,LTD":
                            rootIFD = null;
                            return new OrfDecoder(root, reader);
                        case "SONY":
                            rootIFD = null;
                            return new ArwDecoder(root, reader);
                        case "PENTAX Corporation":
                        case "RICOH IMAGING COMPANY, LTD.":
                        case "PENTAX":
                            rootIFD = null;
                            return new PefDecoder(root, reader);
                        case "Panasonic":
                        case "LEICA":
                            rootIFD = null;
                            return new Rw2Decoder(root, reader);
                        case "SAMSUNG":
                            rootIFD = null;
                            return new SrwDecoder(root, reader);
                        case "Mamiya-OP Co.,Ltd.":
                            rootIFD = null;
                            return new MefDecoder(root, reader);
                        case "Kodak":
                            rootIFD = null;
                            if (String.Compare(model, "DCS560C") == 0)
                                return new Cr2Decoder(root, reader);
                            else
                                return new DcrDecoder(root, reader);
                        case "KODAK":
                            rootIFD = null;
                            return new DcsDecoder(root, reader);
                        case "EASTMAN KODAK COMPANY":
                            rootIFD = null;
                            return new KdcDecoder(root, reader);
                        case "SEIKO EPSON CORP.":
                            rootIFD = null;
                            return new ErfDecoder(root, reader);
                        case "Hasselblad":
                            rootIFD = null;
                            return new ThreefrDecoder(root, reader);
                        case "Leaf":
                            rootIFD = null;
                            return new MosDecoder(root, reader);
                        case "Phase One A/S":
                            rootIFD = null;
                            return new MosDecoder(root, reader);
                            */
                    }
                }

                /*
                // Last ditch effort to identify Leaf cameras that don't have a Tiff Make set
                potentials = rootIFD.getIFDsWithTag(TagType.SOFTWARE);
                if (potentials.Count > 0)
                {
                    potentials[0].tags.TryGetValue((ushort)TagType.SOFTWARE, out Tag tag);
                    string software = tag.dataAsString;
                    software = software.Trim();
                    if (String.Compare(software, "Camera Library") == 0)
                    {
                        rootIFD = null;
                        return new MosDecoder(root, reader);
                    }
                }*/

                //default as astandard tiff
                rootIFD = null;
                return new TiffDecoder(root, ref reader);
            }
            //TODO add detection of Tiff
            throw new TiffParserException("No decoder found. Sorry.");
        }
    }
}

