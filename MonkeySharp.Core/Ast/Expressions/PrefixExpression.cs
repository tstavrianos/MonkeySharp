namespace MonkeySharp.Core.Ast.Expressions
{
    public class PrefixExpression(Token token, string @operator, Expression right) : Expression(token)
    {
        public string Operator { get; } = @operator;
        public Expression Right { get; } = right;

        public override string ToString()
        {
            return $"({Operator}{Right})";
        }
    }
}