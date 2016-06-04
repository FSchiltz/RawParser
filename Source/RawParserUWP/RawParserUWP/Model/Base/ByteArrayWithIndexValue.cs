namespace RawParserUWP.Model.Format.Base
{
    public class uShortArrayWithIndexAsDefaultValue
    {
            ushort?[] arr;

            public uShortArrayWithIndexAsDefaultValue(ushort size)
            {
                arr = new ushort?[size];
            }

            public ushort this[int index]
            {
                get
                {
                    if (arr[index] == null)
                        arr[index] = (ushort)index;

                    return (ushort)arr[index];
                }
                set
                {
                    arr[index] = value;
                }
            }        
    }
}
