using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents an index access expression in the AST, used for accessing elements
/// in arrays or hash maps (e.g., array[index] or hash[key]).
/// </summary>
/// <param name="token">The token representing this expression.</param>
/// <param name="left">The expression being indexed (e.g., the array or hash).</param>
/// <param name="index">The expression used as the index or key.</param>
public sealed class IndexExpression(Token token, Expression left, Expression index) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Index;

    /// <summary>
    /// Gets the left-hand side expression that is being indexed.
    /// This typically represents an array, hash, or other indexable data structure.
    /// </summary>
    public Expression Left { get; } = left;

    /// <summary>
    /// Gets the index expression used to access an element.
    /// This can be an integer for arrays or any expression for hash keys.
    /// </summary>
    public Expression Index { get; } = index;

    /// <summary>
    /// Returns a string representation of the index expression in the format: (left[index]).
    /// </summary>
    /// <returns>A string representation of this index expression.</returns>
    public override string ToString()
    {
        return $"({Left}[{Index}])";
    }

    /// <summary>
    /// Accepts a visitor that does not return a value, implementing the Visitor pattern.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that returns a value of type T, implementing the Visitor pattern.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitor's processing of this expression.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}