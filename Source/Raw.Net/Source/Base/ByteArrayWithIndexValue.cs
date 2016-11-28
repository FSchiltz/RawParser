namespace RawNet
{
    public class UShortArrayWithIndexAsDefaultValue
    {
            ushort?[] arr;

            public UShortArrayWithIndexAsDefaultValue(ushort size)
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
