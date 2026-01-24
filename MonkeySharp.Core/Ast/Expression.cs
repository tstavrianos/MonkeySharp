using MonkeySharp.Core.Ast.Visitors;

namespace MonkeySharp.Core.Ast
{
    public abstract class Expression(Token token) : Node(token)
    {
        public abstract void Accept(IExpressionVisitor visitor);
        public abstract T Accept<T>(IExpressionVisitor<T> visitor);
    }
}