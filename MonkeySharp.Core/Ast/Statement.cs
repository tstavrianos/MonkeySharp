using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast
{
    public abstract class Statement(Token token) : Node(token)
    {
        public abstract void Accept(IStatementVisitor visitor);
        public abstract T Accept<T>(IStatementVisitor<T> visitor);
    }
}