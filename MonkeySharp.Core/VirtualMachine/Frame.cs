using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.VirtualMachine
{
    public class Frame
    {
        public ClosureObject Closure { get; }
        public int Ip { get; set; }
        public int BasePointer { get; }

        internal Frame(ClosureObject closure, int basePointer)
        {
            Closure = closure;
            Ip = -1;
            BasePointer = basePointer;
        }

        public byte[] Instructions()
        {
            return Closure.Function.Instructions;
        }
    }
}