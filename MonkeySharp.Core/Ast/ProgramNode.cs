using System.Collections.Generic;

namespace MonkeySharp.Core.Ast
{
    public class ProgramNode : Node
    {
        public ProgramNode(IReadOnlyList<Statement> statements) : base(default)
        {
            Statements = statements;
        }

        public IReadOnlyList<Statement> Statements { get; }
        public override string TokenLiteral => Statements.Count > 0 ? Statements[0].TokenLiteral : string.Empty;

        public override string ToString()
        {
            return string.Join("", Statements);
        }
    }
}