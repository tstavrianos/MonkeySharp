using System.Collections.Generic;
using System.Linq;

namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string CompiledFunction = "COMPILED_FUNCTION_OBJ";
    }

    public class CompiledFunctionObject : IObject
    {
        internal CompiledFunctionObject(IEnumerable<byte> instructions, int numLocals, int numParameters)
        {
            Instructions = instructions.ToArray();
            NumLocals = numLocals;
            NumParameters = numParameters;
        }

        public byte[] Instructions { get; }
        public int NumLocals { get; }
        public int NumParameters { get; }

        public string Type => ObjectType.CompiledFunction;
        public string Inspect => $"CompiledFunction[{GetHashCode()}]";
    }
}