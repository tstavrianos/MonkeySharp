namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Boolean = "BOOLEAN";
    }

    public class BooleanObject : IHashableObject
    {
        public static readonly BooleanObject False = new(false);
        public static readonly BooleanObject True = new(true);
        private readonly ulong _hash;

        private BooleanObject(bool value)
        {
            Value = value;
            _hash = (ulong) (value ? 1 : 0);
        }

        public string Type => ObjectType.Boolean;
        public bool Value { get; }

        public string Inspect => Value.ToString().ToLower();

        public HashKey HashKey()
        {
            return new HashKey(Type, _hash);
        }
    }
}