using System.Collections.Generic;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Represents the root node of the abstract syntax tree (AST) for a MonkeySharp program.
/// Contains a collection of top-level statements that make up the program.
/// </summary>
/// <param name="statements">The collection of statements that comprise the program.</param>
public class ProgramNode(IReadOnlyList<Statement> statements) : Node(default)
{
    /// <summary>
    /// Gets the read-only collection of statements that make up the program.
    /// </summary>
    public IReadOnlyList<Statement> Statements { get; } = statements;

    /// <summary>
    /// Gets the token literal from the first statement in the program, or an empty string if no statements exist.
    /// </summary>
    public override string TokenLiteral => Statements.Count > 0 ? Statements[0].TokenLiteral : string.Empty;

    /// <summary>
    /// Returns a string representation of the program by concatenating all statements.
    /// </summary>
    /// <returns>A string containing all statements in the program.</returns>
    public override string ToString()
    {
        return string.Join("", Statements);
    }
}