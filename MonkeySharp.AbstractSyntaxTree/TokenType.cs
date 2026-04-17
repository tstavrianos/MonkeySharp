using System;

namespace MonkeySharp.AbstractSyntaxTree;

internal enum TokenType
{
    Illegal,
    EndOfFile,

    Identifier,
    Integer,
    String,

    Assign,
    Plus,
    Minus,
    Bang,
    Asterisk,
    Slash,

    LessThan,
    GreaterThan,

    Comma,
    Semicolon,
    Colon,

    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,

    Function,
    Let,
    True,
    False,
    If,
    Else,
    Return,

    Equal,
    NotEqual,
}

internal static class TokenTypeExtensions
{
    public static string String(this TokenType type)
    {
        return type switch
        {
            TokenType.Illegal => "ILLEGAL",
            TokenType.EndOfFile => "EOF",
            TokenType.Identifier => "IDENT",
            TokenType.Integer => "INT",
            TokenType.String => "STRING",
            TokenType.Assign => "=",
            TokenType.Plus => "+",
            TokenType.Comma => ",",
            TokenType.Semicolon => ";",
            TokenType.LeftParen => "(",
            TokenType.RightParen => ")",
            TokenType.LeftBrace => "{",
            TokenType.RightBrace => "}",
            TokenType.LeftBracket => "[",
            TokenType.RightBracket => "]",
            TokenType.Function => "FUNCTION",
            TokenType.Let => "LET",
            TokenType.Minus => "-",
            TokenType.Bang => "!",
            TokenType.Asterisk => "*",
            TokenType.Slash => "/",
            TokenType.LessThan => "<",
            TokenType.GreaterThan => ">",
            TokenType.True => "true",
            TokenType.False => "false",
            TokenType.If => "if",
            TokenType.Else => "else",
            TokenType.Return => "return",
            TokenType.Equal => "==",
            TokenType.NotEqual => "!=",
            TokenType.Colon => ":",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
