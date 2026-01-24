using System;

namespace MonkeySharp.Core
{
    public enum TokenType
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
                case TokenType.EndOfFile:
                    return "EOF";
                case TokenType.Identifier:
                    return "IDENT";
                case TokenType.Integer:
                    return "INT";
                case TokenType.String:
                    return "STRING";
                case TokenType.Assign:
                    return "=";
                case TokenType.Plus:
                    return "+";
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
                case TokenType.LeftBracket:
                    return "[";
                case TokenType.RightBracket:
                    return "]";
                case TokenType.Function:
                    return "FUNCTION";
                case TokenType.Let:
                    return "LET";
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
                case TokenType.True:
                    return "true";
                case TokenType.False:
                    return "false";
                case TokenType.If:
                    return "if";
                case TokenType.Else:
                    return "else";
                case TokenType.Return:
                    return "return";
                case TokenType.Equal:
                    return "==";
                case TokenType.NotEqual:
                    return "!=";
                case TokenType.Colon:
                    return ":";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}