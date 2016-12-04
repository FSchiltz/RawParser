// dcraw.net - camera raw file decoder
// Copyright (C) 1997-2008  Dave Coffin, dcoffin a cybercom o net
// Copyright (C) 2008-2009  Sam Webster, Dave Brown
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.IO;
using System.Text;

namespace dcraw
{
    public sealed class RawStream : Stream
    {
        private readonly byte[] data;
        private long position;

        public override long Position
        {
            get { return position; }
            set { position = value; }
        }

        public RawStream(Stream stream)
        {
            data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
        }

        public RawStream(string filename)
        {
            data = File.ReadAllBytes(filename);
        }

        public RawStream(byte[] inData, int offset, int count)
        {
            data = new byte[count];
            Array.Copy(inData, offset, data, 0, count);
        }

        public override void Flush()
        {
            // TODO: support for read only?
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position += offset;
                    break;
                case SeekOrigin.End:
                    position = data.Length - offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }

            return position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 0) throw new ArgumentOutOfRangeException("count");

            int totalBytes = Math.Max(Math.Min(count, (int)(data.Length - Position)), 0);

            Array.Copy(data, (int)Position, buffer, offset, totalBytes);
            Position += totalBytes;

            return totalBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get { return data.Length; }
        }

        public override int ReadByte()
        {
            return data[position++];
        }

        public short Order
        {
            set { order = value; }
            get { return order; }
        }

        public short sget2()
        {
            int ret;

            if (order == 0x4949)
            {
                ret = data[position++];
                ret |= data[position++] << 8;
            }
            else
            {
                ret = data[position++] << 8;
                ret |= data[position++];
            }

            return (short)ret;
        }

        public ushort get2()
        {
            return (ushort)sget2();
        }

        public uint get4()
        {
            uint ret;
            if (order == 0x4949)
            {
                ret = data[position++];
                ret |= (uint)data[position++] << 8;
                ret |= (uint)data[position++] << 16;
                ret |= (uint)data[position++] << 24;
            }
            else
            {
                ret = (uint)data[position++] << 24;
                ret |= (uint)data[position++] << 16;
                ret |= (uint)data[position++] << 8;
                ret |= data[position++];
            }

            return ret;
        }

        public int sget4()
        {
            return (int)get4();
        }


        private uint bitbuf;
        private int vbits;
        private bool reset;
        private short order;

        /*
        getbits(n) where 0 <= n <= 25 returns an n-bit integer
        */
        public uint GetBits(int nbits)
        {
            if (nbits == 0 || reset) return 0;

            if (vbits < nbits)
            {
                if (!FillBucket(nbits))
                {
                    return 0;
                }
            }

            vbits -= nbits;
            return bitbuf << (32 - nbits - vbits) >> (32 - nbits);
        }

        public uint PeekBits(int nbits)
        {
            if (nbits == 0 || reset) return 0;

            if (vbits < nbits)
            {
                if (!FillBucket(nbits))
                {
                    return 0;
                }
            }

            int tempvbits = vbits - nbits;
            return bitbuf << (32 - nbits - tempvbits) >> (32 - nbits);
        }

        private bool zero_after_ff;

        public bool ZeroAfterFF
        {
            get { return zero_after_ff; }
            set { zero_after_ff = value; }
        }

        private bool FillBucket(int nbits)
        {
            while (vbits < nbits)
            {
                int c = data[position++];

                reset = zero_after_ff && c == 0xff && data[position++] != 0;

                if (reset)
                {
                    return false;
                }

                bitbuf = (bitbuf << 8) + (byte)c;
                vbits += 8;
            }

            return true;
        }

        public string ReadString(int bytes)
        {
            byte[] chars = new byte[bytes];

            Read(chars, 0, bytes);
            int len = bytes;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == 0)
                {
                    len = i;
                    break;
                }
            }

            return Encoding.ASCII.GetString(chars, 0, len);
        }

        public void ReadShorts(ushort[] array, int count)
        {
            for (int i = 0; i < count; i++)
            {
                array[i] = get2();
            }
            //todo??
            //if ((order == 0x4949) == (ntohs(0x1234) == 0x1234))
            //    swab (pixel, pixel, count*2);
        }

        public double getreal(int type)
        {
            double d;
            switch (type)
            {
                case 3:
                    return get2();
                case 4:
                    return get4();
                case 5:
                    d = get4();
                    return d / get4();
                case 8:
                    return (short)get2();
                case 9:
                    return (int)get4();
                case 10:
                    d = (int)get4();
                    return d / (int)get4();
                case 11:
                    return Utils.IntToFloat(sget4());
                case 12:
                    throw new NotImplementedException();
                /*
            rev = 7 * ((order == 0x4949) ? 1 : 0);
            for (i=0; i < 8; i++)
            {
                u.c[i ^ rev] = ReadByte();
            }
            return u.d;
             */
                default:
                    return ReadByte();
            }
        }

        public uint getint(int type)
        {
            return type == 3 ? get2() : get4();
        }

        public string fgets(int MaxCount)
        {
            int c;
            int count = 0;
            StringBuilder sb = new StringBuilder();
            bool terminated = false;
            do
            {
                c = ReadByte();
                if (c == 0) terminated = true;
                if (c != '\n' && c != '\r')
                {
                    if (!terminated) sb.Append((char)c);
                }
                count++;
            } while (count < (MaxCount - 1) && c != '\n' && c != '\r');
            // TODO skip \n if \r\n

            return sb.ToString();
        }

        public void ResetBits()
        {
            bitbuf = 0;
            vbits = 0;
            reset = false;
        }
    }
}
