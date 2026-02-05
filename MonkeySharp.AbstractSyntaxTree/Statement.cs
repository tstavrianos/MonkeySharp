using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Represents an abstract base class for all statement nodes in the Abstract Syntax Tree (AST).
/// Statements are executable units that perform actions but do not produce values.
/// This class implements the Visitor pattern to allow double dispatch for statement processing.
/// </summary>
/// <param name="token">The token associated with this statement node.</param>
public abstract class Statement(Token token) : Node(token)
{
    /// <summary>
    /// Gets the type of this statement, which categorizes the statement node.
    /// </summary>
    public abstract StatementType StatementType { get; }

    /// <summary>
    /// Accepts a visitor for processing this statement node without returning a value.
    /// This method implements the Visitor pattern to enable double dispatch for statement traversal.
    /// </summary>
    /// <param name="visitor">The statement visitor that will process this node.</param>
    public abstract void Accept(IStatementVisitor visitor);

    /// <summary>
    /// Accepts a visitor for processing this statement node and returns a value of type <typeparamref name="T"/>.
    /// This method implements the Visitor pattern to enable double dispatch for statement traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The statement visitor that will process this node and return a value.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    public abstract T Accept<T>(IStatementVisitor<T> visitor);
}