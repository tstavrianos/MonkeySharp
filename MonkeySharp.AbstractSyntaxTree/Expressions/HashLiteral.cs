using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents a hash (dictionary) literal expression in the abstract syntax tree.
/// Hash literals are collections of key-value pairs enclosed in curly braces, e.g., {"key": "value", 1: 2}.
/// </summary>
/// <param name="token">The token representing the hash literal's opening brace.</param>
/// <param name="pairs">The dictionary of key-value expression pairs that make up the hash literal.</param>
internal sealed class HashLiteral(Token token, IReadOnlyDictionary<Expression, Expression> pairs)
    : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Hash;

    /// <summary>
    /// Gets the read-only dictionary of expression pairs that constitute the key-value mappings of this hash literal.
    /// If the provided pairs parameter is null, an empty dictionary is used.
    /// </summary>
    public IReadOnlyDictionary<Expression, Expression> Pairs { get; } =
        pairs ?? new Dictionary<Expression, Expression>();

    /// <summary>
    /// Returns a string representation of the hash literal in the format {key1:value1, key2:value2, ...}.
    /// </summary>
    /// <returns>A string representation of the hash literal.</returns>
    public override string ToString()
    {
        var buffer = new StringBuilder();
        buffer.Append('{');
        buffer.Append(string.Join(", ", Pairs.Select(x => $"{x.Key}:{x.Value}")));
        buffer.Append('}');
        return buffer.ToString();
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this hash literal expression.
    /// This method implements the Visitor pattern for traversing the AST.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this hash literal expression and returns a result.
    /// This method implements the Visitor pattern for traversing the AST with a return value.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>The result of the visitor's operation on this hash literal.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}