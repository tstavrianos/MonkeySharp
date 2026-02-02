using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.VirtualMachine;

public readonly struct ByteCode
{
    public readonly byte[] Instructions;
    public readonly Value[] Constants;

    internal ByteCode(byte[] instructions, Value[] constants)
    {
        Instructions = instructions;
        Constants = constants;
    }
}