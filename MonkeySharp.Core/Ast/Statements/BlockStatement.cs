using System.Collections.Generic;
using System.Text;

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
    }
}