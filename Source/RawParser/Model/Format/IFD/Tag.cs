namespace RawParser.Format.IFD
{
    public class Tag
    {
        public ushort tagId { get; set; }
        public ushort dataType;
        public uint dataCount;
        public uint dataOffset;
        public object[] data;
        public string displayName { get; set; }
        public string dataAsString
        {
            get
            {
                if (data != null)
                {
                    string temp = "";
                    switch (dataType)
                    {
                        case 1:
                        case 6:
                        case 7:
                            foreach (object t in data)
                            {
                                temp += (byte)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 2:
                            temp = (string)data[0];
                            break;
                        case 3:
                            foreach (object t in data)
                            {
                                temp += (ushort)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 4:
                            foreach (object t in data)
                            {
                                temp += (uint)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 8:
                            foreach (object t in data)
                            {
                                temp += (short)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 9:
                            foreach (object t in data)
                            {
                                temp += (int)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 11:
                            foreach (object t in data)
                            {
                                temp += (int)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;

                        case 5:
                        case 10:
                        case 12:
                            foreach (object t in data)
                            {
                                temp += (double)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                    }
                    return temp;
                }
                else return "";
            }
        }

        public Tag()
        {
            data = new object[1];
            dataCount = 1;
            dataType = 1;
            displayName = "";

        }
        public int getTypeSize(ushort id)
        {
            int size = 0;
            switch (id)
            {
                case 1:
                case 2:
                case 6:
                case 7:
                    size = 1;
                    break;
                case 3:
                case 8:
                    size = 2;
                    break;
                case 4:
                case 9:
                case 11:
                    size = 4;
                    break;
                case 10:
                case 5:
                case 12:
                    size = 8;
                    break;
            }
            return size;
        }
    }
}
