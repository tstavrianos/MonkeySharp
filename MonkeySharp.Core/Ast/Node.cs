namespace MonkeySharp.Core.Ast
{
    public abstract class Node(Token token)
    {
        internal Token Token { get; } = token;

        public virtual string TokenLiteral => Token.Literal;
    }
}