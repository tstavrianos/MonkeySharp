using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class StringLiteral(Token token, string currentLiteral) : Expression(token)
    {
        public string Value { get; } = currentLiteral;

        public override string ToString()
        {
            return Token.Literal;
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