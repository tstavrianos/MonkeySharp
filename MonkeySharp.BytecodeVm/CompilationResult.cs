using System.Collections.Generic;

namespace MonkeySharp.BytecodeVm;

/// <summary>
/// Represents the result of compiling Monkey source code for the bytecode virtual machine.
/// </summary>
public sealed class CompilationResult
{
    internal Bytecode? ByteCode { get; }
    internal IReadOnlyList<string> BuiltinNames { get; }

    /// <summary>
    /// Gets compiler diagnostics produced during parsing or compilation.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// Gets a value that indicates whether compilation succeeded.
    /// </summary>
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
