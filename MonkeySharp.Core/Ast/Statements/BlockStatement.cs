using System.Collections.Generic;
using System.Text;
using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast.Statements
{
    public class BlockStatement(Token token, IReadOnlyList<Statement> statements) : Statement(token)
    {
        public IReadOnlyList<Statement> Statements { get; } = statements ?? [];

        public override string ToString()
        {
            var ret = new StringBuilder();
            foreach (var statement in Statements) ret.Append(statement.ToString());
            return ret.ToString();
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