namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Integer = "INTEGER";
    }

    public class IntegerObject : IHashableObject
    {
        private readonly ulong _hash;

        internal IntegerObject(long value)
        {
            Value = value;
            _hash = (ulong) value;
        }

        public string Type => ObjectType.Integer;
        public long Value { get; }

        public string Inspect => Value.ToString();

        public HashKey HashKey()
        {
            return new HashKey(Type, _hash);
        }
    }
}