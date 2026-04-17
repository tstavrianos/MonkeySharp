using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.TreeWalk;

public sealed class CompilationResult
{
    internal ProgramNode? ProgramNode { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0 && ProgramNode is not null;

    internal CompilationResult(ProgramNode? programNode, IReadOnlyList<string> diagnostics)
    {
        ProgramNode = programNode;
        Diagnostics = diagnostics;
    }
}
