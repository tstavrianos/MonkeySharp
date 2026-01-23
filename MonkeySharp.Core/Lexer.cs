using System.Collections.Generic;

namespace MonkeySharp.Core
{
    public class Lexer
    {
        private static readonly Dictionary<string, TokenType> Keywords = new()
        {
            {"fn", TokenType.Function},
            {"let", TokenType.Let},
            {"true", TokenType.True},
            {"false", TokenType.False},
            {"if", TokenType.If},
            {"else", TokenType.Else},
            {"return", TokenType.Return}
        };

        private readonly string _input;
        private int _position;
        private int _readPosition;
        private char _ch;

        public Lexer(string input)
        {
            _input = input;
            ReadChar();
        }

        private void ReadChar()
        {
            if (_readPosition >= _input.Length)
                _ch = '\0';
            else
                _ch = _input[_readPosition];
            _position = _readPosition;
            _readPosition += 1;
        }

        public Token NextToken()
        {
            Token tok;

            SkipWhitespace();

            switch (_ch)
            {
                case '=':
                    if (PeekChar() == '=')
                    {
                        ReadChar();
                        tok = new Token(TokenType.Equal, "==");
                    }
                    else
                    {
                        tok = NewToken(TokenType.Assign, _ch);
                    }

                    break;
                case '+':
                    tok = NewToken(TokenType.Plus, _ch);
                    break;
                case '-':
                    tok = NewToken(TokenType.Minus, _ch);
                    break;
                case '!':
                    if (PeekChar() == '=')
                    {
                        ReadChar();
                        tok = new Token(TokenType.NotEqual, "!=");
                    }
                    else
                    {
                        tok = NewToken(TokenType.Bang, _ch);
                    }

                    break;
                case '/':
                    tok = NewToken(TokenType.Slash, _ch);
                    break;
                case '*':
                    tok = NewToken(TokenType.Asterisk, _ch);
                    break;
                case '<':
                    tok = NewToken(TokenType.LessThan, _ch);
                    break;
                case '>':
                    tok = NewToken(TokenType.GreaterThan, _ch);
                    break;
                case ';':
                    tok = NewToken(TokenType.Semicolon, _ch);
                    break;
                case '(':
                    tok = NewToken(TokenType.LeftParen, _ch);
                    break;
                case ')':
                    tok = NewToken(TokenType.RightParen, _ch);
                    break;
                case ',':
                    tok = NewToken(TokenType.Comma, _ch);
                    break;
                case '{':
                    tok = NewToken(TokenType.LeftBrace, _ch);
                    break;
                case '}':
                    tok = NewToken(TokenType.RightBrace, _ch);
                    break;
                case '\0':
                    tok = new Token(TokenType.Eof, "");
                    break;
                default:
                    if (IsLetter(_ch))
                    {
                        var literal = ReadIdentifier();
                        tok = new Token(LookupIdent(literal), literal);
                        return tok;
                    }

                    if (IsDigit(_ch))
                    {
                        tok = new Token(TokenType.Integer, ReadNumber());
                        return tok;
                    }

                    tok = NewToken(TokenType.Illegal, _ch);

                    break;
            }

            ReadChar();
            return tok;
        }

        private static Token NewToken(TokenType type, char ch)
        {
            return new Token(type, ch.ToString());
        }

        private string ReadIdentifier()
        {
            var position = _position;
            while (IsLetter(_ch)) ReadChar();
            return _input.Substring(position, _position - position);
        }

        private static bool IsLetter(char ch)
        {
            return ch is >= 'a' and <= 'z' || ch is >= 'A' and <= 'Z' || ch == '_';
        }

        private static TokenType LookupIdent(string ident)
        {
            return Keywords.GetValueOrDefault(ident, TokenType.Identifier);
        }

        private void SkipWhitespace()
        {
            while (_ch == ' ' || _ch == '\t' || _ch == '\n' || _ch == '\r') ReadChar();
        }

        private static bool IsDigit(char ch)
        {
            return ch is >= '0' and <= '9';
        }

        private string ReadNumber()
        {
            var position = _position;
            while (IsDigit(_ch)) ReadChar();
            return _input.Substring(position, _position - position);
        }

        private char PeekChar()
        {
            if (_readPosition >= _input.Length)
                return '\0';
            return _input[_readPosition];
        }
    }
}