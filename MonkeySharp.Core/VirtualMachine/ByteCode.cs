using System.Collections.Generic;
using System.Linq;
using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.VirtualMachine
{
    public readonly struct ByteCode
    {
        public readonly byte[] Instructions;
        public readonly IObject[] Constants;

        internal ByteCode(byte[] instructions, IObject[] constants)
        {
            Instructions = instructions;
            Constants = constants;
        }
    }
}