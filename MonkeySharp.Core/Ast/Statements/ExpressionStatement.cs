using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Statements
{
    public class ExpressionStatement(Token token, Expression expression) : Statement(token)
    {
        public Expression Expression { get; } = expression;

        public override string ToString()
        {
            return Expression != null ? Expression.ToString() : string.Empty;
        }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}