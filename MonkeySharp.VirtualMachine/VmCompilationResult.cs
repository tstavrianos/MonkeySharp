using System;
using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.VirtualMachine;

public sealed class VmCompilationResult
{
    internal ByteCode? ByteCode { get; }
    internal IReadOnlyList<string> BuiltinNames { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0 && ByteCode is not null;

    internal VmCompilationResult(
        ByteCode? byteCode,
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<string> builtinNames
    )
    {
        ByteCode = byteCode;
        Diagnostics = diagnostics;
        BuiltinNames = builtinNames;
    }
}
