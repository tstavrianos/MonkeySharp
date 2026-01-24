using System.Collections.Generic;
using System.Linq;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class ArrayLiteral(Token token, IReadOnlyList<Expression> elements) : Expression(token)
    {
        public IReadOnlyList<Expression> Elements { get; } = elements ?? [];

        public override string ToString()
        {
            return $"[{string.Join(", ", Elements.Select(x => x.ToString()))}]";
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