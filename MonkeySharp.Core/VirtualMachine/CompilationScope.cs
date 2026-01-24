using System.Collections.Generic;

namespace MonkeySharp.Core.VirtualMachine
{
    public class CompilationScope
    {
        public List<byte> Instructions { get; }
        public EmittedInstruction LastInstruction { get; set; }
        public EmittedInstruction PreviousInstruction { get; set; }

        internal CompilationScope()
        {
            Instructions = [];
            LastInstruction = default;
            PreviousInstruction = default;
        }
    }
}