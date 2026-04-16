namespace MonkeySharp.VirtualMachine;

internal readonly struct EmittedInstruction
{
    public readonly OpCode OpCode;
    public readonly int Position;

    internal EmittedInstruction(OpCode opCode, int position)
    {
        OpCode = opCode;
        Position = position;
    }
}
