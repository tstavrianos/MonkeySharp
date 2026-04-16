using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents an infix expression in the abstract syntax tree.
/// An infix expression consists of two operands and an operator between them (e.g., "x + y", "a * b").
/// </summary>
/// <param name="token">The token associated with this infix expression.</param>
/// <param name="left">The left operand expression.</param>
/// <param name="operator">The operator string (e.g., "+", "-", "*", "/", "==", "!=").</param>
/// <param name="right">The right operand expression.</param>
internal sealed class InfixExpression(Token token, Expression left, string @operator, Expression right)
    : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Infix;

    /// <summary>
    /// Gets the left operand of the infix expression.
    /// </summary>
    public Expression Left { get; } = left;

    /// <summary>
    /// Gets the operator of the infix expression (e.g., "+", "-", "*", "/", "==", "!=").
    /// </summary>
    public string Operator { get; } = @operator;

    /// <summary>
    /// Gets the right operand of the infix expression.
    /// </summary>
    public Expression Right { get; } = right;

    /// <summary>
    /// Returns a string representation of the infix expression in the format "(left operator right)".
    /// </summary>
    /// <returns>A string representation of the infix expression with parentheses.</returns>
    public override string ToString()
    {
        return $"({Left} {Operator} {Right})";
    }

    /// <summary>
    /// Accepts an expression visitor that does not return a value.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts an expression visitor that returns a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitor's visit operation.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}