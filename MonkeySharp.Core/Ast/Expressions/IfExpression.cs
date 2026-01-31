using System.Text;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions;

/// <summary>
/// Represents an if-else conditional expression in the abstract syntax tree.
/// </summary>
/// <param name="token">The token representing the 'if' keyword.</param>
/// <param name="condition">The boolean expression that determines which branch to execute.</param>
/// <param name="consequence">The block statement to execute when the condition is true.</param>
/// <param name="alternative">The block statement to execute when the condition is false (optional).</param>
public class IfExpression(
    Token token,
    Expression condition,
    BlockStatement consequence,
    BlockStatement alternative)
    : Expression(token)
{
    /// <summary>
    /// Gets or sets the boolean expression that determines which branch to execute.
    /// </summary>
    public Expression Condition { get; set; } = condition;

    /// <summary>
    /// Gets or sets the block statement to execute when the condition evaluates to true.
    /// </summary>
    public BlockStatement Consequence { get; set; } = consequence;

    /// <summary>
    /// Gets or sets the block statement to execute when the condition evaluates to false.
    /// Can be null if there is no else clause.
    /// </summary>
    public BlockStatement Alternative { get; set; } = alternative;

    /// <summary>
    /// Returns a string representation of the if-else expression.
    /// </summary>
    /// <returns>A string in the format "if[condition] [consequence]" or "if[condition] [consequence]else [alternative]".</returns>
    public override string ToString()
    {
        var ret = new StringBuilder();
        ret.Append("if");
        ret.Append(Condition);
        ret.Append(' ');
        ret.Append(Consequence);
        if (Alternative != null)
        {
            ret.Append("else ");
            ret.Append(Alternative);
        }

        return ret.ToString();
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this if expression node.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor that performs operations on this if expression node and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result produced by the visitor.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}