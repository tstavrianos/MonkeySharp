using MonkeySharp.VirtualMachine.Objects;

namespace MonkeySharp.VirtualMachine;

internal readonly struct ByteCode
{
    public readonly byte[] Instructions;
    public readonly Value[] Constants;

    internal ByteCode(byte[] instructions, Value[] constants)
    {
        Instructions = instructions;
        Constants = constants;
    }
}
