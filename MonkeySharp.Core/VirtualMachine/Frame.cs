using MonkeySharp.Core.Objects;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.VirtualMachine;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Instructions()
    {
        return Closure.Function.Instructions;
    }
}

public class ValueFrame
{
    public Value Closure { get; }
    public int Ip { get; set; }
    public int BasePointer { get; }

    internal ValueFrame(Value closure, int basePointer)
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