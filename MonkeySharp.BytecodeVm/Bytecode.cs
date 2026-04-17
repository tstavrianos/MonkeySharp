using MonkeySharp.BytecodeVm.Objects;

namespace MonkeySharp.BytecodeVm;

internal readonly struct Bytecode
{
    public readonly byte[] Instructions;
    public readonly Value[] Constants;

    internal Bytecode(byte[] instructions, Value[] constants)
    {
        Instructions = instructions;
        Constants = constants;
    }
}
