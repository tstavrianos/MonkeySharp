using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Statements;

/// <summary>
/// Represents a let statement in the abstract syntax tree.
/// A let statement binds a value to an identifier (e.g., "let x = 5;").
/// </summary>
/// <param name="token">The token associated with this statement (the 'let' keyword).</param>
/// <param name="name">The identifier being bound in this let statement.</param>
/// <param name="value">The expression whose value will be assigned to the identifier.</param>
public class LetStatement(Token token, Identifier name, Expression value) : Statement(token)
{
    /// <summary>
    /// Gets the identifier that is being bound in this let statement.
    /// </summary>
    public Identifier Name { get; } = name;

    /// <summary>
    /// Gets the expression representing the value to be assigned to the identifier.
    /// </summary>
    public Expression Value { get; } = value;

    /// <summary>
    /// Returns a string representation of the let statement in source code format.
    /// </summary>
    /// <returns>A string in the format "let name = value;".</returns>
    public override string ToString()
    {
        return $"{TokenLiteral} {Name} = {Value?.ToString() ?? string.Empty};";
    }

    /// <summary>
    /// Accepts a visitor for processing this statement without a return value.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IStatementVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor for processing this statement with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitor's processing.</returns>
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}