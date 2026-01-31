using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions;

/// <summary>
/// Represents an integer literal expression in the abstract syntax tree.
/// </summary>
/// <param name="token">The token representing the integer literal in the source code.</param>
/// <param name="value">The parsed integer value.</param>
public class IntegerLiteral(Token token, long value) : Expression(token)
{
    /// <summary>
    /// Gets the integer value of this literal.
    /// </summary>
    public long Value { get; } = value;

    /// <summary>
    /// Returns the string representation of the integer literal.
    /// </summary>
    /// <returns>The literal text from the source code.</returns>
    public override string ToString()
    {
        return Token.Literal;
    }

    /// <summary>
    /// Accepts a visitor that processes this integer literal expression.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that processes this integer literal expression and returns a result.
    /// </summary>
    /// <typeparam name="T">The return type of the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result produced by the visitor.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}