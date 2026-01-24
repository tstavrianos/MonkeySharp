namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Error = "ERROR";
    }

    public class ErrorObject : IObject
    {
        internal ErrorObject(string message)
        {
            Message = message;
        }

        public string Type => ObjectType.Error;
        public string Message { get; }
        public string Inspect => $"ERROR: {Message}";
    }
}