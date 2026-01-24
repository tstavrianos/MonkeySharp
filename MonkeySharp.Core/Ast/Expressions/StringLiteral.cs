namespace MonkeySharp.Core.Ast.Expressions
{
    public class StringLiteral(Token token, string currentLiteral) : Expression(token)
    {
        public string Value { get; } = currentLiteral;

        public override string ToString()
        {
            return Token.Literal;
        }
    }
}