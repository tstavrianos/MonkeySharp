using System.Collections.Generic;

namespace MonkeySharp.BytecodeVm;

public sealed class CompilationResult
{
    internal Bytecode? ByteCode { get; }
    internal IReadOnlyList<string> BuiltinNames { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0 && ByteCode is not null;

    internal CompilationResult(
        Bytecode? byteCode,
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<string> builtinNames
    )
    {
        ByteCode = byteCode;
        Diagnostics = diagnostics;
        BuiltinNames = builtinNames;
    }
}
