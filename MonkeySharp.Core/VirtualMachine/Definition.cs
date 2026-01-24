namespace MonkeySharp.Core.VirtualMachine
{
    public readonly struct Definition
    {
        public readonly string Name;
        public readonly int[] OperandWidths;

        internal Definition(string name, int[] operandWidths)
        {
            Name = name;
            OperandWidths = operandWidths;
        }
    }
}