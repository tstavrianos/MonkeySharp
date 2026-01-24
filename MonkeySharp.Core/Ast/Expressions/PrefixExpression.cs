using MonkeySharp.Core.Ast.Visitors;

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

        public override void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}