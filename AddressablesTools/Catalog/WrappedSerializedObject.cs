namespace AddressablesTools.Catalog
{
    public class WrappedSerializedObject
    {
        public SerializedType Type { get; set; }
        public object Object { get; set; }

        public WrappedSerializedObject(SerializedType type, object obj)
        {
            Type = type;
            Object = obj;
        }
    }
}
