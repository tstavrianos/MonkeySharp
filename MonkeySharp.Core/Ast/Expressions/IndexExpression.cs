using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class IndexExpression(Token token, Expression left, Expression index) : Expression(token)
    {
        public Expression Left { get; } = left;
        public Expression Index { get; } = index;

        public override string ToString()
        {
            return $"({Left}[{Index}])";
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