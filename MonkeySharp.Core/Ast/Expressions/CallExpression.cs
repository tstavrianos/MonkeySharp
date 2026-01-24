using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class CallExpression(Token token, Expression function, IReadOnlyList<Expression> arguments)
        : Expression(token)
    {
        public Expression Function { get; } = function;
        public IReadOnlyList<Expression> Arguments { get; } = arguments ?? [];

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append(Function);
            ret.Append('(');
            ret.Append(string.Join(", ", Arguments.Select(x => x.ToString())));
            ret.Append(")");
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