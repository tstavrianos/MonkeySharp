using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.Interpreter;

public sealed class ParseResult
{
    internal ProgramNode? ProgramNode { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0 && ProgramNode is not null;

    internal ParseResult(ProgramNode? programNode, IReadOnlyList<string> diagnostics)
    {
        ProgramNode = programNode;
        Diagnostics = diagnostics;
    }
}
