using System;
using System.Linq;
namespace RawNet
{
    public enum CFAColor
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        CYAN = 4,
        MAGENTA = 5,
        YELLOW = 6,
        WHITE = 7,
        COLOR_MAX = 8,
        FUJI_GREEN = 9,
        Unknow = 255
    };

    public class ColorFilterArray
    {
        public Point2D Size { get; set; }
        public CFAColor[] cfa;

        public ColorFilterArray(Point2D size)
        {
            SetSize(size);
        }

        public ColorFilterArray()
        {
        }

        public ColorFilterArray(ColorFilterArray other)
        {
            cfa = null;
            SetSize(other.Size);
            if (cfa != null)
                Common.Memcopy(cfa, other.cfa, Size.Area);
        }

        public override string ToString()
        {
            string val = "";
            for (int i = 0; i < (Size.Height * Size.Width); i++)
            {
                val += cfa[i].ToString().First();
            }
            return val;
        }
        /*

        public ColorFilterArray(uint filters)
        {
            Size = new Point2D(8, 2);
            cfa = null;
            SetSize(Size);

            for (uint x = 0; x < 8; x++)
            {
                for (uint y = 0; y < 2; y++)
                {
                    CFAColor c = (CFAColor)FC(filters, y, x);
                    SetColorAt(new Point2D(x, y), c);
                }
            }
        }

        // FC macro from dcraw outputs, given the filters definition, the dcraw color
        // number for that given position in the CFA pattern
        protected static uint FC(uint filters, uint row, uint col)
        {
            return ((filters) >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3);
        }*/

        public ColorFilterArray Equal(ColorFilterArray other)
        {
            SetSize(other.Size);
            if (cfa != null)
                Common.Memcopy(cfa, other.cfa, Size.Area * sizeof(CFAColor));
            return this;
        }

        public void SetSize(Point2D size)
        {
            Size = size;
            cfa = null;
            if (Size.Area > 100)
                throw new RawDecoderException("if your CFA pattern is really " + Size.Area + " pixels in area we may as well give up now");
            if (Size.Area <= 0)
                return;
            cfa = new CFAColor[Size.Area];
            if (cfa == null)
                throw new RawDecoderException("Unable to allocate memory");
        }

        public CFAColor GetColorAt(uint x, uint y)
        {
            if (cfa == null)
                throw new RawDecoderException("No CFA size set");
            if (x >= Size.Width || y >= Size.Height)
            {
                x = x % Size.Width;
                y = y % Size.Height;
            }
            return cfa[x + y * Size.Width];
        }

        public void SetCFA(Point2D inSize, CFAColor color1, CFAColor color2, CFAColor color3, CFAColor color4)
        {
            if (inSize != Size)
            {
                SetSize(inSize);
            }

            cfa[0] = color1;
            cfa[1] = color2;
            cfa[2] = color3;
            cfa[3] = color4;
        }


        public void ShiftLeft(uint count)
        {
            if (Size.Width == 0)
            {
                throw new RawDecoderException("No CFA size set (or set to zero)");
            }
            uint shift = count % Size.Width;
            if (0 == shift)
                return;
            CFAColor[] newCFa = new CFAColor[Size.Width * Size.Height];
            CFAColor[] tmp = new CFAColor[Size.Width];
            for (int y = 0; y < Size.Height; y++)
            {
                CFAColor[] oldfirst = cfa.Skip((int)(y * Size.Width)).ToArray().Take((int)count).ToArray();
                CFAColor[] oldlast = cfa.Skip((int)(y * Size.Width + count)).Take((int)(Size.Width - count)).ToArray();
                int i = 0;
                for (; i < count; i++)
                {
                    newCFa[(int)(y * Size.Width) + i] = oldfirst[i];
                }
                for (; i < Size.Width; i++)
                {
                    newCFa[(int)(y * Size.Width) + i] = oldlast[i - count];
                }
            }
        }

        public void ShiftDown(uint count)
        {
            if (Size.Height == 0)
            {
                throw new RawDecoderException("No CFA size set (or set to zero)");
            }
            uint shift = count % Size.Height;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[Size.Height];
            for (int x = 0; x < Size.Width; x++)
            {
                CFAColor[] old = cfa.Skip(x).ToArray();
                for (int y = 0; y < Size.Height; y++)
                    tmp[y] = old[((y + shift) % Size.Height) * Size.Width];
                for (int y = 0; y < Size.Height; y++)
                    old[y * Size.Width] = tmp[y];
            }
        }

        protected string AsString()
        {
            string dst = "";
            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    dst += (GetColorAt((uint)x, (uint)y)).ToString();
                    dst += (x == Size.Width - 1) ? "\n" : ",";
                }
            }
            return dst;
        }

        public void SetColorAt(Point2D position, CFAColor color)
        {
            if (position.Width >= Size.Width || position.Width < 0)
                throw new RawDecoderException("Position out of CFA pattern");
            if (position.Height >= Size.Height || position.Height < 0)
                throw new RawDecoderException("Position out of CFA pattern");
            cfa[position.Width + position.Height * Size.Width] = color;
        }
    };
}
