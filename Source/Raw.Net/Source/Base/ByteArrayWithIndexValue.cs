namespace RawNet
{
    internal class UShortArrayWithIndexAsDefaultValue
    {
            ushort?[] arr;

            protected UShortArrayWithIndexAsDefaultValue(ushort size)
            {
                arr = new ushort?[size];
            }

            protected ushort this[int index]
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
