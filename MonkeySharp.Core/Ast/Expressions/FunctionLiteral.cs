using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Expressions
{
    public class FunctionLiteral(Token token, IReadOnlyList<Identifier> parameters, BlockStatement body)
        : Expression(token)
    {
        public string Name { get; set; }
        public IReadOnlyList<Identifier> Parameters { get; } = parameters ?? [];
        public BlockStatement Body { get; set; } = body;

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append(TokenLiteral);
            if (!string.IsNullOrEmpty(Name))
                ret.Append($"<{Name}>");
            ret.Append('(');
            ret.Append(string.Join(", ", Parameters.Select(x => x.ToString())));
            ret.Append(") ");
            ret.Append(Body);
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