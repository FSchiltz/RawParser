using System;
using System.Linq;
namespace RawNet
{
    public enum CFAColor
    {
        RED = 0,
        GREEN = 1,
        BLUE = 2,
        CYAN = 4,
        MAGENTA = 5,
        YELLOW = 6,
        WHITE = 7,
        COLOR_MAX = 8,
        FUJI_GREEN = 9,
        UNKNOWN = 255
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
                Common.Memcopy(cfa, other.cfa, Size.Area());
        }

        public override string ToString()
        {
            string val = "";
            for (int i = 0; i < (Size.height * Size.width); i++)
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
                Common.Memcopy(cfa, other.cfa, Size.Area() * sizeof(CFAColor));
            return this;
        }

        public void SetSize(Point2D size)
        {
            Size = size;
            cfa = null;
            if (Size.Area() > 100)
                throw new RawDecoderException("ColorFilterArray:setSize if your CFA pattern is really " + Size.Area() + " pixels in area we may as well give up now");
            if (Size.Area() <= 0)
                return;
            cfa = new CFAColor[Size.Area()];
            if (cfa == null)
                throw new RawDecoderException("ColorFilterArray:setSize Unable to allocate memory");
        }

        public CFAColor GetColorAt(uint x, uint y)
        {
            if (cfa == null)
                throw new RawDecoderException("ColorFilterArray:getColorAt: No CFA size set");
            if (x >= Size.width || y >= Size.height)
            {
                x = x % Size.width;
                y = y % Size.height;
            }
            return cfa[x + y * Size.width];
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
            if (Size.width == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftLeft: No CFA size set (or set to zero)");
            }
            //Debug.Write("Shift left:" + n + "\n");
            uint shift = count % Size.width;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[Size.width];
            for (int y = 0; y < Size.height; y++)
            {
                CFAColor[] old = cfa.Skip((int)(y * Size.width)).ToArray();
                Common.Memcopy(tmp, old, (Size.width - shift) * sizeof(CFAColor), 0, (int)shift);
                Common.Memcopy(tmp, old, shift * sizeof(CFAColor), (int)(Size.width - shift), 0);
                Common.Memcopy(old, tmp, Size.width * sizeof(CFAColor));
            }
        }

        public void ShiftDown(uint count)
        {
            if (Size.height == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftDown: No CFA size set (or set to zero)");
            }
            //Debug.Write("Shift down:" + n + "\n");
            uint shift = count % Size.height;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[Size.height];
            for (int x = 0; x < Size.width; x++)
            {
                CFAColor[] old = cfa.Skip(x).ToArray();
                for (int y = 0; y < Size.height; y++)
                    tmp[y] = old[((y + shift) % Size.height) * Size.width];
                for (int y = 0; y < Size.height; y++)
                    old[y * Size.width] = tmp[y];
            }
        }

        protected string AsString()
        {
            string dst = "";
            for (int y = 0; y < Size.height; y++)
            {
                for (int x = 0; x < Size.width; x++)
                {
                    dst += (GetColorAt((uint)x, (uint)y)).ToString();
                    dst += (x == Size.width - 1) ? "\n" : ",";
                }
            }
            return dst;
        }

        public void SetColorAt(Point2D position, CFAColor color)
        {
            if (position.width >= Size.width || position.width < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            if (position.height >= Size.height || position.height < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            cfa[position.width + position.height * Size.width] = color;
        }
    };
}
