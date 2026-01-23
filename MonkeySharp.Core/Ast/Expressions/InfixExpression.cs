namespace MonkeySharp.Core.Ast.Expressions
{
    public class InfixExpression(Token token, Expression left, string @operator, Expression right)
        : Expression(token)
    {
        public Expression Left { get; } = left;
        public string Operator { get; } = @operator;
        public Expression Right { get; } = right;

        public override string ToString()
        {
            return $"({Left} {Operator} {Right})";
        }
    }
}