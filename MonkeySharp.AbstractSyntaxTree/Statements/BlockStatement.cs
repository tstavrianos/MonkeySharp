using System.Collections.Generic;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Statements;

/// <summary>
/// Represents a block statement in the MonkeySharp AST, which is a collection of statements enclosed in curly braces.
/// Block statements are used in control structures, function bodies, and other contexts where multiple statements are grouped together.
/// </summary>
/// <param name="token">The token representing the opening brace of the block.</param>
/// <param name="statements">The collection of statements contained within the block. If null, an empty collection is used.</param>
public sealed class BlockStatement(Token token, IReadOnlyList<Statement> statements) : Statement(token)
{
    /// <summary>
    /// Gets the type of this statement, which categorizes the statement node.
    /// </summary>
    public override StatementType StatementType => StatementType.Block;

    /// <summary>
    /// Gets the read-only collection of statements contained within this block.
    /// </summary>
    /// <value>
    /// A read-only list of <see cref="Statement"/> objects. Returns an empty collection if no statements were provided.
    /// </value>
    public IReadOnlyList<Statement> Statements { get; } = statements ?? [];

    /// <summary>
    /// Returns a string representation of the block statement by concatenating
    /// the string representations of all contained statements.
    /// </summary>
    /// <returns>A string containing all the statements in the block.</returns>
    public override string ToString()
    {
        var ret = new StringBuilder();
        foreach (var statement in Statements) ret.Append(statement.ToString());
        return ret.ToString();
    }

    /// <summary>
    /// Accepts a visitor for the visitor pattern implementation, allowing traversal and processing of the AST.
    /// </summary>
    /// <param name="visitor">The statement visitor to accept.</param>
    public override void Accept(IStatementVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a generic visitor for the visitor pattern implementation, allowing traversal and processing of the AST with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The generic statement visitor to accept.</param>
    /// <returns>The result of the visitor's processing of this block statement.</returns>
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}