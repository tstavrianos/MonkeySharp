using System.Collections.Generic;
using System.Linq;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class ArrayLiteral(Token token, IReadOnlyList<Expression> elements) : Expression(token)
    {
        public IReadOnlyList<Expression> Elements { get; } = elements ?? [];

        public override string ToString()
        {
            return $"[{string.Join(", ", Elements.Select(x => x.ToString()))}]";
        }
    }
}