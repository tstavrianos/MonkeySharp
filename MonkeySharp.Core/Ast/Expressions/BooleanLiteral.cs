using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions;

/// <summary>
/// Represents a boolean literal expression in the abstract syntax tree.
/// This class uses the flyweight pattern with pre-defined instances for true and false values.
/// </summary>
public class BooleanLiteral : Expression
{
    /// <summary>
    /// Gets the singleton instance representing a boolean true literal.
    /// </summary>
    public static readonly BooleanLiteral True = new(new Token(TokenType.True, "true"), true);

    /// <summary>
    /// Gets the singleton instance representing a boolean false literal.
    /// </summary>
    public static readonly BooleanLiteral False = new(new Token(TokenType.False, "false"), false);

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanLiteral"/> class.
    /// </summary>
    /// <param name="token">The token representing this boolean literal in the source code.</param>
    /// <param name="value">The boolean value of this literal.</param>
    private BooleanLiteral(Token token, bool value) : base(token)
    {
        Value = value;
    }


    /// <summary>
    /// Gets the boolean value of this literal.
    /// </summary>
    public bool Value { get; }

    /// <summary>
    /// Returns a string representation of this boolean literal.
    /// </summary>
    /// <returns>The literal text representation of the boolean value.</returns>
    public override string ToString()
    {
        return Token.Literal;
    }

    /// <summary>
    /// Accepts a visitor for traversing the expression tree without returning a value.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <summary>
    /// Accepts a visitor for traversing the expression tree and returns a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of visiting this boolean literal.</returns>
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}