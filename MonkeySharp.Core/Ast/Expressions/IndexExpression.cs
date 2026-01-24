namespace MonkeySharp.Core.Ast.Expressions
{
    public class IndexExpression(Token token, Expression left, Expression index) : Expression(token)
    {
        public Expression Left { get; } = left;
        public Expression Index { get; } = index;

        public override string ToString()
        {
            return $"({Left}[{Index}])";
        }
    }
}