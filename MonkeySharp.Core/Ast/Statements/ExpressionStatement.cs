namespace MonkeySharp.Core.Ast.Statements
{
    public class ExpressionStatement(Token token, Expression expression) : Statement(token)
    {
        public Expression Expression { get; } = expression;

        public override string ToString()
        {
            return Expression != null ? Expression.ToString() : string.Empty;
        }
    }
}