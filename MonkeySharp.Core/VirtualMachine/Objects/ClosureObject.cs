namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Closure = "CLOSURE";
    }

    public class ClosureObject : IObject
    {
        internal ClosureObject(CompiledFunctionObject function, IObject[] free)
        {
            Function = function;
            Free = free;
        }

        public CompiledFunctionObject Function { get; }
        public IObject[] Free { get; }
        public string Type => ObjectType.Closure;
        public string Inspect => $"Closure[{GetHashCode()}]";
    }
}