using MonkeySharp.Core.Objects;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.VirtualMachine;

public class Frame
{
    public Value Closure { get; }
    public int Ip { get; set; }
    public int BasePointer { get; }

    internal Frame(Value closure, int basePointer)
    {
        Closure = closure;
        Ip = -1;
        BasePointer = basePointer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Instructions()
    {
        return Closure.ClosureData.Function.CompiledFunctionData.Instructions;
    }
}