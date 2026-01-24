using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class Identifier(Token token, string value) : Expression(token)
    {
        public string Value { get; } = value;

        public override string ToString()
        {
            return Value;
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