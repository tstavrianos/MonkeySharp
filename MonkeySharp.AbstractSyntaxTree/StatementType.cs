namespace MonkeySharp.AbstractSyntaxTree;

public enum StatementType : byte
{
    Block = 1,
    Expression = 2,
    Let = 3,
    Return = 4
}