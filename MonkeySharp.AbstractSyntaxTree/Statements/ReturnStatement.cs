using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Statements;

/// <summary>
/// Represents a return statement in the abstract syntax tree.
/// A return statement returns a value from a function (e.g., "return x + 5;").
/// </summary>
/// <param name="token">The token associated with this statement (the 'return' keyword).</param>
/// <param name="returnValue">The expression whose value will be returned.</param>
internal sealed class ReturnStatement(Token token, Expression returnValue) : Statement(token)
{
    /// <summary>
    /// Gets the type of this statement, which categorizes the statement node.
    /// </summary>
    public override StatementType StatementType => StatementType.Return;

    /// <summary>
    /// Gets the expression representing the value to be returned.
    /// </summary>
    public Expression ReturnValue { get; } = returnValue;

    /// <summary>
    /// Returns a string representation of the return statement in source code format.
    /// </summary>
    /// <returns>A string in the format "return value;" where value is the string representation of the return expression.</returns>
    public override string ToString()
    {
        return $"{TokenLiteral} {ReturnValue};";
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
