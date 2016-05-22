namespace RawParser.Model.Format
{
    class Tag
    {
        public ushort tagId { get; set; }
        public ushort dataType;
        public uint dataCount;
        public uint dataOffset;
        public object[] data { get; set; }
        public string dataAsString
        {
            get
            {
                string temp = "";
                switch (dataType)
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
                        temp
                        break;
                    case 10:
                    case 5:
                    case 12:
                        temp = 
                        break;
                }
                return temp;
            }
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
