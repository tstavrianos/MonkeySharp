using System.Text;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class IfExpression(
        Token token,
        Expression condition,
        BlockStatement consequence,
        BlockStatement alternative)
        : Expression(token)
    {
        public Expression Condition { get; set; } = condition;
        public BlockStatement Consequence { get; set; } = consequence;
        public BlockStatement Alternative { get; set; } = alternative;

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append("if");
            ret.Append(Condition);
            ret.Append(' ');
            ret.Append(Consequence);
            if (Alternative != null)
            {
                ret.Append("else ");
                ret.Append(Alternative);
            }

            return ret.ToString();
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