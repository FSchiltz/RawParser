using PhotoNet.Common;
using RawNet.Decoder.HuffmanCompressor;
using RawNet.Format.Tiff;

namespace RawNet.Decoder.Decompressor
{

    class PentaxDecompressor : JPEGDecompressor
    {
        public PentaxDecompressor(ImageBinaryReader file, RawImage img) : base(file, img, false, false)
        {
            huff[0] = new PentaxHuffman();
        }

        public void DecodePentax(IFD root, uint offset, uint size)
        {
            // Prepare huffmann table              0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 = 16 entries
            byte[] pentax_tree =  { 0, 2, 3, 1, 1, 1, 1, 1, 1, 2, 0, 0, 0, 0, 0, 0,
                                         3, 4, 2, 5, 1, 6, 0, 7, 8, 9, 10, 11, 12
                                       };
            //                                     0 1 2 3 4 5 6 7 8 9  0  1  2 = 13 entries        

            /* Attempt to read huffman table, if found in makernote */
            Tag t = root.GetEntryRecursive((TagType)0x220);
            if (t != null)
            {
                if (t.dataType == TiffDataType.UNDEFINED)
                {
                    ImageBinaryReader stream;
                    if (root.endian == Common.GetHostEndianness())
                        stream = new ImageBinaryReader(t.GetByteArray());
                    else
                        stream = new ImageBinaryReaderBigEndian(t.GetByteArray());

                    int depth = (stream.ReadUInt16() + 12) & 0xf;

                    stream.ReadBytes(12);
                    uint[] v0 = new uint[16];
                    uint[] v1 = new uint[16];
                    uint[] v2 = new uint[16];
                    for (int i = 0; i < depth; i++)
                        v0[i] = stream.ReadUInt16();

                    for (int i = 0; i < depth; i++)
                        v1[i] = stream.ReadByte();

                    /* Reset bits */
                    for (int i = 0; i < 17; i++)
                        huff[0].bits[i] = 0;

                    /* Calculate codes and store bitcounts */
                    for (int c = 0; c < depth; c++)
                    {
                        v2[c] = v0[c] >> (int)(12 - v1[c]);
                        huff[0].bits[v1[c]]++;
                    }
                    /* Find smallest */
                    for (int i = 0; i < depth; i++)
                    {
                        uint sm_val = 0xfffffff;
                        uint sm_num = 0xff;
                        for (uint j = 0; j < depth; j++)
                        {
                            if (v2[j] <= sm_val)
                            {
                                sm_num = j;
                                sm_val = v2[j];
                            }
                        }
                        huff[0].huffval[i] = sm_num;
                        v2[sm_num] = 0xffffffff;
                    }
                    stream.Dispose();
                }
                else
                {
                    throw new RawDecoderException("Unknown Huffman table type.");
                }
            }
            else
            {
                /* Initialize with legacy data */
                uint acc = 0;
                for (int i = 0; i < 16; i++)
                {
                    huff[0].bits[i + 1] = pentax_tree[i];
                    acc += huff[0].bits[i + 1];
                }
                huff[0].bits[0] = 0;
                for (int i = 0; i < acc; i++)
                {
                    huff[0].huffval[i] = pentax_tree[i + 16];
                }
            }
            huff[0].UseBigTable = true;
            huff[0].Create(frame.precision);

            input.BaseStream.Position = 0;
            huff[0].bitPump = new BitPumpMSB(input, offset, size);
            int[] pUp1 = { 0, 0 };
            int[] pUp2 = { 0, 0 };
            int pLeft1 = 0;
            int pLeft2 = 0;

            for (int y = 0; y < raw.fullSize.dim.height; y++)
            {
                var realY = y * raw.fullSize.dim.width;
                pUp1[y & 1] += huff[0].Decode();
                pUp2[y & 1] += huff[0].Decode();
                raw.fullSize.rawView[realY] = (ushort)(pLeft1 = pUp1[y & 1]);
                raw.fullSize.rawView[realY + 1] = (ushort)(pLeft2 = pUp2[y & 1]);
                for (int x = 2; x < raw.fullSize.dim.width; x += 2)
                {
                    pLeft1 += huff[0].Decode();
                    pLeft2 += huff[0].Decode();
                    raw.fullSize.rawView[realY + x] = (ushort)pLeft1;
                    raw.fullSize.rawView[realY + x + 1] = (ushort)pLeft2;
                }

            }
        }
    }
}
