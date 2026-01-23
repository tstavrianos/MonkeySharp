namespace MonkeySharp.Core.Ast.Expressions
{
    public class BooleanLiteral : Expression
    {
        public static readonly BooleanLiteral True = new(new Token(TokenType.True, "true"), true);
        public static readonly BooleanLiteral False = new(new Token(TokenType.False, "false"), false);

        private BooleanLiteral(Token token, bool value) : base(token)
        {
            Value = value;
        }

        public bool Value { get; }

        public override string ToString()
        {
            return Token.Literal;
        }
    }
}