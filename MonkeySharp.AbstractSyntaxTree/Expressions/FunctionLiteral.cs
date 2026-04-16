using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents a function literal expression in the abstract syntax tree.
/// Function literals define anonymous or named functions with parameters and a body.
/// </summary>
/// <param name="token">The token associated with this function literal (typically the 'fn' keyword).</param>
/// <param name="parameters">The list of parameter identifiers for the function.</param>
/// <param name="body">The block statement containing the function's body.</param>
internal sealed class FunctionLiteral(
    Token token,
    IReadOnlyList<Identifier> parameters,
    BlockStatement body
) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Function;

    /// <summary>
    /// Gets or sets the optional name of the function.
    /// This is primarily used for named function expressions or debugging purposes.
    /// This property is mutable to support AST transformations and optimizations.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the read-only list of parameter identifiers for this function.
    /// </summary>
    public IReadOnlyList<Identifier> Parameters { get; } = parameters;

    /// <summary>
    /// Gets or sets the block statement containing the executable body of the function.
    /// </summary>
    public BlockStatement Body { get; set; } = body;

    /// <summary>
    /// Returns a string representation of the function literal.
    /// The format is: fn&lt;name&gt;(param1, param2, ...) { body }
    /// where &lt;name&gt; is included only if the function has a name.
    /// </summary>
    /// <returns>A string representation of the function literal.</returns>
    public override string ToString()
    {
        var ret = new StringBuilder();
        ret.Append(TokenLiteral);
        if (!string.IsNullOrEmpty(Name))
            ret.Append($"<{Name}>");
        ret.Append('(');
        ret.Append(string.Join(", ", Parameters.Select(x => x.ToString())));
        ret.Append(") ");
        ret.Append(Body);
        return ret.ToString();
    }

    /// <summary>
    /// Accepts a visitor for the visitor pattern implementation.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a generic visitor that returns a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the visitor.</typeparam>
    /// <param name="visitor">The generic expression visitor to accept.</param>
    /// <returns>The result of the visitor's visit operation.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}
