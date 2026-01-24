using MonkeySharp.Core.Ast.Expressions;

namespace MonkeySharp.Core.Ast.Visitors
{
    public interface IExpressionVisitor
    {
        void Visit(Identifier identifier);
        void Visit(IntegerLiteral integerLiteral);
        void Visit(PrefixExpression prefixExpression);
        void Visit(InfixExpression infixExpression);
        void Visit(BooleanLiteral booleanLiteral);
        void Visit(IfExpression ifExpression);
        void Visit(FunctionLiteral functionLiteral);
        void Visit(CallExpression callExpression);
        void Visit(StringLiteral stringLiteral);
        void Visit(ArrayLiteral arrayLiteral);
        void Visit(IndexExpression indexExpression);
        void Visit(HashLiteral hashLiteral);
    }

    public interface IExpressionVisitor<out T>
    {
        T Visit(Identifier identifier);
        T Visit(IntegerLiteral integerLiteral);
        T Visit(PrefixExpression prefixExpression);
        T Visit(InfixExpression infixExpression);
        T Visit(BooleanLiteral booleanLiteral);
        T Visit(IfExpression ifExpression);
        T Visit(FunctionLiteral functionLiteral);
        T Visit(CallExpression callExpression);
        T Visit(StringLiteral stringLiteral);
        T Visit(ArrayLiteral arrayLiteral);
        T Visit(IndexExpression indexExpression);
        T Visit(HashLiteral hashLiteral);
    }
}