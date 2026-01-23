namespace MonkeySharp.Core.Ast.Expressions
{
    public class Identifier(Token token, string value) : Expression(token)
    {
        public string Value { get; } = value;

        public override string ToString()
        {
            return Value;
        }
    }
}