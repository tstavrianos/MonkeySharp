namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Null = "NULL";
    }

    public class NullObject : IObject
    {
        public static readonly NullObject Null = new();

        private NullObject()
        {
        }

        public string Type => ObjectType.Null;
        public string Inspect => "null";
    }
}