namespace MonkeySharp.Core
{
    public readonly struct Token
    {
        public readonly TokenType Type;
        public readonly string Literal;

        internal Token(TokenType type, string literal)
        {
            Type = type;
            Literal = literal;
        }
    }
}