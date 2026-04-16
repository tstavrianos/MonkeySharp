using System.Collections.Generic;

namespace MonkeySharp.VirtualMachine;

internal sealed class CompilationScope
{
    internal List<byte> Instructions { get; }
    internal EmittedInstruction LastInstruction { get; set; }
    internal EmittedInstruction PreviousInstruction { get; set; }

    internal CompilationScope()
    {
        Instructions = [];
        LastInstruction = default;
        PreviousInstruction = default;
    }
}
