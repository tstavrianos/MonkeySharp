using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents a prefix expression in the abstract syntax tree.
/// A prefix expression consists of an operator applied before an operand (e.g., -5, !true).
/// </summary>
/// <param name="token">The token representing the prefix operator.</param>
/// <param name="operator">The prefix operator string (e.g., "-", "!", "+").</param>
/// <param name="right">The expression that the prefix operator is applied to.</param>
internal sealed class PrefixExpression(Token token, string @operator, Expression right) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Prefix;

    /// <summary>
    /// Gets the prefix operator (e.g., "-", "!", "+").
    /// </summary>
    public string Operator { get; } = @operator;

    /// <summary>
    /// Gets the right-hand expression that the prefix operator is applied to.
    /// </summary>
    public Expression Right { get; } = right;

    /// <summary>
    /// Returns a string representation of the prefix expression in the format: (operator right).
    /// </summary>
    /// <returns>A string representation of the prefix expression.</returns>
    public override string ToString()
    {
        return $"({Operator}{Right})";
    }

    /// <summary>
    /// Accepts a visitor that implements the <see cref="IExpressionVisitor"/> interface,
    /// allowing the visitor to process this prefix expression node.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that implements the <see cref="IExpressionVisitor{T}"/> interface,
    /// allowing the visitor to process this prefix expression node and return a result.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitor's processing of this prefix expression.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}