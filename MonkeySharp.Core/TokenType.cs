using System;

namespace MonkeySharp.Core
{
    public enum TokenType
    {
        Illegal,
        Eof,

        Identifier,
        Integer,

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

        LeftParen,
        RightParen,
        LeftBrace,
        RightBrace,

        Function,
        Let,
        True,
        False,
        If,
        Else,
        Return,

        Equal,
        NotEqual
    }

    public static class TokenTypeExtensions
    {
        public static string String(this TokenType type)
        {
            switch (type)
            {
                case TokenType.Illegal:
                    return "ILLEGAL";
                case TokenType.Eof:
                    return "EOF";
                case TokenType.Identifier:
                    return "IDENT";
                case TokenType.Integer:
                    return "INT";
                case TokenType.Assign:
                    return "=";
                case TokenType.Plus:
                    return "+";
                case TokenType.Minus:
                    return "-";
                case TokenType.Bang:
                    return "!";
                case TokenType.Asterisk:
                    return "*";
                case TokenType.Slash:
                    return "/";
                case TokenType.LessThan:
                    return "<";
                case TokenType.GreaterThan:
                    return ">";
                case TokenType.Comma:
                    return ",";
                case TokenType.Semicolon:
                    return ";";
                case TokenType.LeftParen:
                    return "(";
                case TokenType.RightParen:
                    return ")";
                case TokenType.LeftBrace:
                    return "{";
                case TokenType.RightBrace:
                    return "}";
                case TokenType.Function:
                    return "FUNCTION";
                case TokenType.Let:
                    return "LET";
                case TokenType.True:
                    return "TRUE";
                case TokenType.False:
                    return "FALSE";
                case TokenType.If:
                    return "IF";
                case TokenType.Else:
                    return "ELSE";
                case TokenType.Return:
                    return "RETURN";
                case TokenType.Equal:
                    return "==";
                case TokenType.NotEqual:
                    return "!=";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}