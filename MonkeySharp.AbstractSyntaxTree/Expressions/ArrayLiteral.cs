using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents an array literal expression in the abstract syntax tree.
/// Array literals are collections of expressions enclosed in square brackets, e.g., [1, 2, 3].
/// </summary>
/// <param name="token">The token representing the array literal's opening bracket.</param>
/// <param name="elements">The collection of expressions that make up the array elements.</param>
internal sealed class ArrayLiteral(Token token, IReadOnlyList<Expression> elements) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Array;

    /// <summary>
    /// Gets the read-only list of expressions that constitute the elements of this array literal.
    /// </summary>
    public IReadOnlyList<Expression> Elements { get; } = elements;

    /// <summary>
    /// Returns a string representation of the array literal in the format [element1, element2, ...].
    /// </summary>
    /// <returns>A string representation of the array literal.</returns>
    public override string ToString()
    {
        return $"[{string.Join(", ", Elements.Select(x => x.ToString()))}]";
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this array literal expression.
    /// This method implements the Visitor pattern for traversing the AST.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this array literal expression and returns a result.
    /// This method implements the Visitor pattern for traversing the AST with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>The result produced by the visitor's visit operation.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}