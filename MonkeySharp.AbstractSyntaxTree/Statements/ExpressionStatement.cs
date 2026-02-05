using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Statements;

/// <summary>
/// Represents a statement that consists of a single expression in the MonkeySharp abstract syntax tree.
/// Expression statements are statements where an expression is evaluated for its side effects.
/// </summary>
/// <param name="token">The token associated with this statement.</param>
/// <param name="expression">The expression that forms this statement.</param>
public sealed class ExpressionStatement(Token token, Expression expression) : Statement(token)
{
    /// <summary>
    /// Gets the type of this statement, which categorizes the statement node.
    /// </summary>
    public override StatementType StatementType => StatementType.Expression;

    /// <summary>
    /// Gets the expression contained within this expression statement.
    /// </summary>
    public Expression Expression { get; } = expression;

    /// <summary>
    /// Returns a string representation of this expression statement.
    /// </summary>
    /// <returns>
    /// The string representation of the contained expression, or an empty string if the expression is null.
    /// </returns>
    public override string ToString()
    {
        return Expression?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Accepts a visitor to perform operations on this expression statement.
    /// This method implements the Visitor pattern for statement traversal.
    /// </summary>
    /// <param name="visitor">The visitor that will process this statement.</param>
    public override void Accept(IStatementVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a generic visitor to perform operations on this expression statement and returns a result.
    /// This method implements the Visitor pattern for statement traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor that will process this statement.</param>
    /// <returns>The result of the visitor's operation on this statement.</returns>
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}