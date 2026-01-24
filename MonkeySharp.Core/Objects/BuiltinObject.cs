using System.Collections.Generic;

namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Builtin = "BUILTIN";
    }

    public class BuiltinObject : IObject
    {
        internal BuiltinObject(BuiltinFunction function)
        {
            Function = function;
        }

        public delegate IObject BuiltinFunction(IReadOnlyList<IObject> args);

        public BuiltinFunction Function { get; }
        public string Type => ObjectType.Builtin;
        public string Inspect => "<builtin function>";
    }
}