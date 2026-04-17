namespace MonkeySharp.AbstractSyntaxTree;

internal enum ExpressionType : byte
{
    Array = 1,
    Boolean = 2,
    Call = 3,
    Function = 4,
    Hash = 5,
    Identifier = 6,
    If = 7,
    Index = 8,
    Infix = 9,
    Integer = 10,
    Prefix = 11,
    String = 12,
}
