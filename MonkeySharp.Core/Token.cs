namespace MonkeySharp.Core
{
    public readonly struct Token(TokenType type, string literal)
    {
        public readonly TokenType Type = type;
        public readonly string Literal = literal;

        public override string ToString()
        {
            return $"{{Type:{Type.String()} Literal:{Literal}}}";
        }
    }
}