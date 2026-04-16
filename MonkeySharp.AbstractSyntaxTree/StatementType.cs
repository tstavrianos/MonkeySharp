namespace MonkeySharp.AbstractSyntaxTree;

internal enum StatementType : byte
{
    Block = 1,
    Expression = 2,
    Let = 3,
    Return = 4
}