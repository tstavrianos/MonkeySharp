using MonkeySharp.Core.Ast.Statements;

namespace MonkeySharp.Core.Ast.Visitors
{
    public interface IStatementVisitor
    {
        void Visit(LetStatement letStatement);
        void Visit(ReturnStatement returnStatement);
        void Visit(ExpressionStatement expressionStatement);
        void Visit(BlockStatement blockStatement);
    }

    public interface IStatementVisitor<out T>
    {
        T Visit(LetStatement letStatement);
        T Visit(ReturnStatement returnStatement);
        T Visit(ExpressionStatement expressionStatement);
        T Visit(BlockStatement blockStatement);
    }
}