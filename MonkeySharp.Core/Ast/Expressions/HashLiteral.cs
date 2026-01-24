using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class HashLiteral(Token token, IReadOnlyDictionary<Expression, Expression> pairs)
        : Expression(token)
    {
        public IReadOnlyDictionary<Expression, Expression> Pairs { get; } =
            pairs ?? new Dictionary<Expression, Expression>();

        public override string ToString()
        {
            var buffer = new StringBuilder();
            buffer.Append('{');
            buffer.Append(string.Join(", ", Pairs.Select(x => $"{x.Key}:{x.Value}")));
            buffer.Append('}');
            return buffer.ToString();
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