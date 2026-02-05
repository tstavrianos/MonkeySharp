using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Represents an abstract base class for all expression nodes in the Abstract Syntax Tree (AST).
/// Expressions are constructs that evaluate to a value.
/// </summary>
/// <param name="token">The token associated with this expression node.</param>
public abstract class Expression(Token token) : Node(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public abstract ExpressionType ExpressionType { get; }

    /// <summary>
    /// Accepts a visitor that processes this expression node without returning a value.
    /// This method implements the Visitor pattern for AST traversal.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public abstract void Accept(IExpressionVisitor visitor);

    /// <summary>
    /// Accepts a visitor that processes this expression node and returns a value of type <typeparamref name="T"/>.
    /// This method implements the Visitor pattern for AST traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}