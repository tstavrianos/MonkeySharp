namespace MonkeySharp.Core.Ast.Statements
{
    public class ReturnStatement(Token token, Expression returnValue) : Statement(token)
    {
        public Expression ReturnValue { get; } = returnValue;

        public override string ToString()
        {
            return $"{TokenLiteral} {(ReturnValue != null ? ReturnValue.ToString() : string.Empty)};";
        }
    }
}