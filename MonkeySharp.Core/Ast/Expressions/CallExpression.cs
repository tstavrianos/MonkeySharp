using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    }
}