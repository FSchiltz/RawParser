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

        // FC macro from dcraw outputs, given the filters definition, the dcraw color
        // number for that given position in the CFA pattern
        protected static uint FC(uint filters, int row, int col)
        {
            return ((filters) >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3);
        }

        public ColorFilterArray(UInt32 filters)
        {
            Size = new Point2D(8, 2);
            cfa = null;
            SetSize(Size);

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    CFAColor c = ToRawspeedColor(FC(filters, y, x));
                    SetColorAt(new Point2D(x, y), c);
                }
            }
        }

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

        public CFAColor GetColorAt(UInt32 x, UInt32 y)
        {
            if (cfa == null)
                throw new RawDecoderException("ColorFilterArray:getColorAt: No CFA size set");
            if (x >= (UInt32)Size.width || y >= (UInt32)Size.height)
            {
                x = (uint)(x % Size.width);
                y = (uint)(y % Size.height);
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

        public void ShiftLeft(int n)
        {
            if (Size.width == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftLeft: No CFA size set (or set to zero)");
            }
            //Debug.Write("Shift left:" + n + "\n");
            int shift = n % Size.width;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[Size.width];
            for (int y = 0; y < Size.height; y++)
            {
                CFAColor[] old = cfa.Skip(y * Size.width).ToArray();
                Common.Memcopy(tmp, old, (uint)((Size.width - shift) * sizeof(CFAColor)), 0, shift);
                Common.Memcopy(tmp, old, (uint)(shift * sizeof(CFAColor)), Size.width - shift, 0);
                Common.Memcopy(old, tmp, (uint)(Size.width * sizeof(CFAColor)));
            }
        }

        public void ShiftDown(int n)
        {
            if (Size.height == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftDown: No CFA size set (or set to zero)");
            }
            //Debug.Write("Shift down:" + n + "\n");
            int shift = n % Size.height;
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

        public void SetColorAt(Point2D pos, CFAColor c)
        {
            if (pos.width >= Size.width || pos.width < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            if (pos.height >= Size.height || pos.height < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            cfa[pos.width + pos.height * Size.width] = c;
        }

        protected UInt32 GetDcrawFilter()
        {
            //dcraw magic
            if (Size.width == 6 && Size.height == 6)
                return 9;

            if (Size.width > 8 || Size.height > 2 || cfa == null)
                return 1;

            if (Math.Log(Size.width, 2) % 1 == 0)
                return 1;

            UInt32 ret = 0;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    UInt32 c = ToDcrawColor(GetColorAt((uint)x, (uint)y));
                    int g = (x >> 1) * 8;
                    ret |= c << ((x & 1) * 2 + y * 4 + g);
                }
            }
            return ret;
        }

        protected static CFAColor ToRawspeedColor(UInt32 dcrawColor)
        {
            switch (dcrawColor)
            {
                case 0: return CFAColor.RED;
                case 1: return CFAColor.GREEN;
                case 2: return CFAColor.BLUE;
                case 3: return CFAColor.GREEN;
            }
            return CFAColor.UNKNOWN;
        }

        protected static UInt32 ToDcrawColor(CFAColor c)
        {
            switch (c)
            {
                case CFAColor.FUJI_GREEN:
                case CFAColor.RED: return 0;
                case CFAColor.MAGENTA:
                case CFAColor.GREEN: return 1;
                case CFAColor.CYAN:
                case CFAColor.BLUE: return 2;
                case CFAColor.YELLOW:
                default:
                    break;
            }
            return 0;
        }
    };
}
