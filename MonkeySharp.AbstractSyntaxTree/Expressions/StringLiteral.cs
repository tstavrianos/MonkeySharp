using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents a string literal expression in the abstract syntax tree.
/// </summary>
/// <param name="token">The token representing the string literal in the source code.</param>
/// <param name="currentLiteral">The parsed string value.</param>
public sealed class StringLiteral(Token token, string currentLiteral) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.String;

    /// <summary>
    /// Gets the string value of this literal.
    /// </summary>
    public string Value { get; } = currentLiteral;

    /// <summary>
    /// Returns the string representation of the string literal.
    /// </summary>
    /// <returns>The literal text from the source code.</returns>
    public override string ToString()
    {
        return Token.Literal;
    }

    /// <summary>
    /// Accepts a visitor that processes this string literal expression.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that processes this string literal expression and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result produced by the visitor.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}