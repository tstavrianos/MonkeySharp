namespace MonkeySharp.Core.Ast.Expressions
{
    public class IntegerLiteral(Token token, long value) : Expression(token)
    {
        public long Value { get; } = value;

        public override string ToString()
        {
            return Token.Literal;
        }
    }
}