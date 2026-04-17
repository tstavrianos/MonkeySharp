using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Expressions;

/// <summary>
/// Represents a function call expression in the abstract syntax tree.
/// Encapsulates the function being called and its argument list.
/// </summary>
/// <param name="token">The token associated with this call expression.</param>
/// <param name="function">The expression representing the function to be called.</param>
/// <param name="arguments">A read-only list of expressions representing the arguments passed to the function.</param>
internal sealed class CallExpression(
    Token token,
    Expression function,
    IReadOnlyList<Expression> arguments
) : Expression(token)
{
    /// <summary>
    /// Gets the type of this expression, which categorizes the expression node.
    /// </summary>
    public override ExpressionType ExpressionType => ExpressionType.Call;

    /// <summary>
    /// Gets the expression representing the function being called.
    /// This can be an identifier, a function literal, or any expression that evaluates to a callable object.
    /// </summary>
    public Expression Function { get; } = function;

    /// <summary>
    /// Gets the read-only list of argument expressions passed to the function call.
    /// </summary>
    public IReadOnlyList<Expression> Arguments { get; } = arguments;

    /// <summary>
    /// Returns a string representation of the call expression in the format: function(arg1, arg2, ...).
    /// </summary>
    /// <returns>A string representing the function call with its arguments.</returns>
    public override string ToString()
    {
        var ret = new StringBuilder();
        ret.Append(Function);
        ret.Append('(');
        ret.Append(string.Join(", ", Arguments.Select(x => x.ToString())));
        ret.Append(')');
        return ret.ToString();
    }

    /// <summary>
    /// Accepts a visitor for the visitor pattern implementation, allowing the visitor to process this call expression.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a generic visitor for the visitor pattern implementation, allowing the visitor to process this call expression and return a result.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The generic expression visitor to accept.</param>
    /// <returns>The result of the visitor's processing of this call expression.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}
