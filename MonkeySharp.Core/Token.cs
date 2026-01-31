namespace MonkeySharp.Core
{
    /// <summary>
    /// Represents a lexical token with a specific type and associated literal value.
    /// </summary>
    /// <remarks>A token is typically produced by a lexer or tokenizer as part of parsing source text. Each
    /// token consists of a type, indicating its syntactic role, and a literal string, representing the exact text
    /// matched in the input.</remarks>
    public readonly record struct Token
    {
        /// <summary>
        /// Represents the type of token associated with this instance.
        /// </summary>
        public TokenType Type { get; }
        /// <summary>
        /// Represents the literal string value associated with this instance.
        /// </summary>
        public string Literal { get; }

        /// <summary>
        /// Initializes a new instance of the Token class with the specified token type and literal value.
        /// </summary>
        /// <param name="type">The type of the token to initialize.</param>
        /// <param name="literal">The literal value associated with the token.</param>
        internal Token(TokenType type, string literal)
        {
            Type = type;
            Literal = literal ?? string.Empty;
        }
    }
}