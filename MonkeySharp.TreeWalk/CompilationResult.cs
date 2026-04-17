using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.TreeWalk;

/// <summary>
/// Represents the result of compiling Monkey source code for the tree-walk engine.
/// </summary>
public sealed class CompilationResult
{
    internal ProgramNode? ProgramNode { get; }

    /// <summary>
    /// Gets compiler diagnostics produced during parsing or compilation.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// Gets a value that indicates whether compilation succeeded.
    /// </summary>
    public bool IsValid => Diagnostics.Count == 0 && ProgramNode is not null;

    internal CompilationResult(ProgramNode? programNode, IReadOnlyList<string> diagnostics)
    {
        ProgramNode = programNode;
        Diagnostics = diagnostics;
    }
}
