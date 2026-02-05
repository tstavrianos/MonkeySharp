using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents an identifier expression in the abstract syntax tree.
/// An identifier is a symbolic name that references a variable, function, or other named entity.
/// </summary>
/// <param name="token">The token associated with this identifier expression.</param>
/// <param name="value">The string value of the identifier name.</param>
public sealed class Identifier(Token token, string value) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Identifier;

    /// <summary>
    /// Gets the string value representing the identifier's name.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Returns a string representation of the identifier.
    /// </summary>
    /// <returns>The identifier's value as a string.</returns>
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    /// Accepts a visitor that processes this identifier expression.
    /// Implements the visitor pattern for AST traversal.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that processes this identifier expression and returns a result.
    /// Implements the visitor pattern for AST traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>The result of the visitor's processing of this identifier.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}