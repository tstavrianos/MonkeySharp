namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string ReturnValue = "RETURN_VALUE";
    }

    public class ReturnValueObject : IObject
    {
        internal ReturnValueObject(IObject value)
        {
            Value = value;
        }

        public string Type => ObjectType.ReturnValue;
        public IObject Value { get; }
        public string Inspect => Value.Inspect;
    }
}