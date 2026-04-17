using System.Runtime.CompilerServices;
using MonkeySharp.BytecodeVm.Objects;

namespace MonkeySharp.BytecodeVm;

internal sealed class Frame
{
    internal Value Closure { get; set; }
    internal int Ip { get; set; }
    internal int BasePointer { get; }

    internal Frame(Value closure, int basePointer)
    {
        Closure = closure;
        Ip = -1;
        BasePointer = basePointer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] Instructions()
    {
        return Closure.ClosureData!.Function.CompiledFunctionData!.Instructions;
    }
}
