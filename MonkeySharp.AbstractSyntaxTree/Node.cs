namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Represents an abstract base node in the Abstract Syntax Tree (AST).
/// All AST nodes inherit from this class to provide common functionality for token management and string representation.
/// </summary>
/// <param name="token">The token associated with this AST node.</param>
internal abstract class Node(Token token)
{
    /// <summary>
    /// Gets the token associated with this AST node.
    /// This token contains the lexical information from the source code that this node represents.
    /// </summary>
    internal Token Token { get; } = token;

    /// <summary>
    /// Gets the literal value of the token associated with this node.
    /// This property provides access to the original text representation from the source code.
    /// </summary>
    public virtual string TokenLiteral => Token.Literal;

    /// <summary>
    /// Returns a string representation of this AST node.
    /// Derived classes must implement this method to provide their specific string representation.
    /// </summary>
    /// <returns>A string that represents the current AST node.</returns>
    public abstract override string ToString();
}