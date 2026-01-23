using MonkeySharp.Core.Ast.Expressions;

namespace MonkeySharp.Core.Ast.Statements
{
    public class LetStatement(Token token, Identifier name, Expression value) : Statement(token)
    {
        public Identifier Name { get; } = name;
        public Expression Value { get; } = value;

        public override string ToString()
        {
            return $"{TokenLiteral} {Name} = {(Value != null ? Value.ToString() : string.Empty)};";
        }
    }
}