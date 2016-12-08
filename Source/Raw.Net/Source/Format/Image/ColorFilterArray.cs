using System;
using System.Diagnostics;
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
        public Point2D size { get; set; }
        public CFAColor[] cfa { get; set; }

        public ColorFilterArray(Point2D _size)
        {
            setSize(_size);
        }

        public ColorFilterArray()
        {
            size = new Point2D(0, 0);
        }

        public ColorFilterArray(ColorFilterArray other)
        {
            cfa = null;
            setSize(other.size);
            if (cfa != null)
                Common.memcopy(cfa, other.cfa, size.area());
        }

        // FC macro from dcraw outputs, given the filters definition, the dcraw color
        // number for that given position in the CFA pattern
        protected uint FC(uint filters, int row, int col)
        {
            return ((filters) >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3);
        }

        public ColorFilterArray(UInt32 filters)
        {
            size = new Point2D(8, 2);
            cfa = null;
            setSize(size);

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    CFAColor c = toRawspeedColor(FC(filters, y, x));
                    setColorAt(new Point2D(x, y), c);
                }
            }
        }

        public ColorFilterArray Equal(ColorFilterArray other)
        {
            setSize(other.size);
            if (cfa != null)
                Common.memcopy(cfa, other.cfa, size.area() * sizeof(CFAColor));
            return this;
        }

        public void setSize(Point2D _size)
        {
            size = _size;
            cfa = null;
            if (size.area() > 100)
                throw new RawDecoderException("ColorFilterArray:setSize if your CFA pattern is really " + size.area() + " pixels in area we may as well give up now");
            if (size.area() <= 0)
                return;
            cfa = new CFAColor[size.area()];
            if (cfa == null)
                throw new RawDecoderException("ColorFilterArray:setSize Unable to allocate memory");
            //Common.memset(cfa, CFAColor.UNKNOWN, (int)(size.area()));
        }

        public CFAColor getColorAt(UInt32 x, UInt32 y)
        {
            if (cfa == null)
                throw new RawDecoderException("ColorFilterArray:getColorAt: No CFA size set");
            if (x >= (UInt32)size.x || y >= (UInt32)size.y)
            {
                x = (uint)(x % size.x);
                y = (uint)(y % size.y);
            }
            return cfa[x + y * size.x];
        }

        public void setCFA(Point2D in_size, CFAColor color1, CFAColor color2, CFAColor color3, CFAColor color4)
        {
            if (in_size != size)
            {
                setSize(in_size);
            }

            cfa[0] = color1;
            cfa[1] = color2;
            cfa[2] = color3;
            cfa[3] = color4;
        }

        public void shiftLeft(int n)
        {
            if (size.x == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftLeft: No CFA size set (or set to zero)");
            }
            Debug.Write("Shift left:" + n + "\n");
            int shift = n % size.x;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[size.x];
            for (int y = 0; y < size.y; y++)
            {
                CFAColor[] old = cfa.Skip(y * size.x).ToArray();
                Common.memcopy(tmp, old, (uint)((size.x - shift) * sizeof(CFAColor)), 0, shift);
                Common.memcopy(tmp, old, (uint)(shift * sizeof(CFAColor)), size.x - shift, 0);
                Common.memcopy(old, tmp, (uint)(size.x * sizeof(CFAColor)));
            }
        }

        public void shiftDown(int n)
        {
            if (size.y == 0)
            {
                throw new RawDecoderException("ColorFilterArray:shiftDown: No CFA size set (or set to zero)");
            }
            Debug.Write("Shift down:" + n + "\n");
            int shift = n % size.y;
            if (0 == shift)
                return;
            CFAColor[] tmp = new CFAColor[size.y];
            for (int x = 0; x < size.x; x++)
            {
                CFAColor[] old = cfa.Skip(x).ToArray();
                for (int y = 0; y < size.y; y++)
                    tmp[y] = old[((y + shift) % size.y) * size.x];
                for (int y = 0; y < size.y; y++)
                    old[y * size.x] = tmp[y];
            }
        }

        protected string asString()
        {
            string dst = "";
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    dst += colorToString(getColorAt((uint)x, (uint)y));
                    dst += (x == size.x - 1) ? "\n" : ",";
                }
            }
            return dst;
        }


        protected string colorToString(CFAColor c)
        {
            switch (c)
            {
                case CFAColor.RED:
                    return "RED";
                case CFAColor.GREEN:
                    return "GREEN";
                case CFAColor.BLUE:
                    return "BLUE";
                case CFAColor.CYAN:
                    return "CYAN";
                case CFAColor.MAGENTA:
                    return "MAGENTA";
                case CFAColor.YELLOW:
                    return "YELLOW";
                case CFAColor.WHITE:
                    return "WHITE";
                case CFAColor.FUJI_GREEN:
                    return "FUJIGREEN";
                default:
                    return "UNKNOWN";
            }
        }


        public void setColorAt(Point2D pos, CFAColor c)
        {
            if (pos.x >= size.x || pos.x < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            if (pos.y >= size.y || pos.y < 0)
                throw new RawDecoderException("SetColor: position out of CFA pattern");
            cfa[pos.x + pos.y * size.x] = c;
        }

        protected UInt32 getDcrawFilter()
        {
            //dcraw magic
            if (size.x == 6 && size.y == 6)
                return 9;

            if (size.x > 8 || size.y > 2 || cfa == null)
                return 1;

            if (Math.Log(size.x, 2) % 1 == 0)
                return 1;

            UInt32 ret = 0;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    UInt32 c = toDcrawColor(getColorAt((uint)x, (uint)y));
                    int g = (x >> 1) * 8;
                    ret |= c << ((x & 1) * 2 + y * 4 + g);
                }
            }
            /*
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    writeLog(DEBUG_PRIO_EXTRA, "%s,", colorToString((CFAColor)toDcrawColor(getColorAt(x, y))).c_str());
                }
                writeLog(DEBUG_PRIO_EXTRA, "\n");
            }
            writeLog(DEBUG_PRIO_EXTRA, "DCRAW filter:%x\n", ret);*/
            return ret;
        }

        protected CFAColor toRawspeedColor(UInt32 dcrawColor)
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

        protected UInt32 toDcrawColor(CFAColor c)
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
