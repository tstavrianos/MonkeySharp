using System.Collections.Generic;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Provides lexical analysis for a source input string, producing tokens for use in parsing and interpretation.
/// </summary>
/// <remarks>The Lexer reads the input character by character and identifies syntactic elements such as
/// keywords, identifiers, operators, literals, and delimiters. It is designed to be used in language processing
/// scenarios where input must be tokenized before further analysis. The Lexer skips whitespace and recognizes both
/// single- and multi-character tokens. When an unrecognized character is encountered, a token of type
/// TokenType.Illegal is produced. The class is not thread-safe and should be used from a single thread at a
/// time.</remarks>
internal class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        { "fn", TokenType.Function },
        { "let", TokenType.Let },
        { "true", TokenType.True },
        { "false", TokenType.False },
        { "if", TokenType.If },
        { "else", TokenType.Else },
        { "return", TokenType.Return },
    };

    private const char NullChar = '\0';
    private const char QuoteChar = '"';

    private readonly string _input;
    private int _position;
    private int _readPosition;
    private char _ch;

    public Lexer(string input)
    {
        _input = input;
        ReadChar();
    }

    /// <summary>
    /// Advances the reading position by one character and updates the current character value.
    /// </summary>
    private void ReadChar()
    {
        if (_readPosition >= _input.Length)
            _ch = NullChar;
        else
            _ch = _input[_readPosition];
        _position = _readPosition;
        _readPosition += 1;
    }

    /// <summary>
    /// Retrieves the next token from the input stream, advancing the lexer position.
    /// </summary>
    /// <remarks>This method skips any leading whitespace before reading the next token. It recognizes
    /// operators, delimiters, identifiers, numbers, and string literals. If an unrecognized character is
    /// encountered, a token of type <see cref="TokenType.Illegal"/> is returned.</remarks>
    /// <returns>A <see cref="Token"/> representing the next syntactic element in the input. Returns a token of type <see
    /// cref="TokenType.EndOfFile"/> when the end of input is reached.</returns>
    public Token NextToken()
    {
        Token token;

        SkipWhitespace();

        switch (_ch)
        {
            case '=':
                if (PeekChar() == '=')
                {
                    ReadChar();
                    token = new Token(TokenType.Equal, "==");
                }
                else
                {
                    token = NewToken(TokenType.Assign, _ch);
                }

                break;
            case '+':
                token = NewToken(TokenType.Plus, _ch);
                break;
            case '-':
                token = NewToken(TokenType.Minus, _ch);
                break;
            case '!':
                if (PeekChar() == '=')
                {
                    ReadChar();
                    token = new Token(TokenType.NotEqual, "!=");
                }
                else
                {
                    token = NewToken(TokenType.Bang, _ch);
                }

                break;
            case '/':
                token = NewToken(TokenType.Slash, _ch);
                break;
            case '*':
                token = NewToken(TokenType.Asterisk, _ch);
                break;
            case '<':
                token = NewToken(TokenType.LessThan, _ch);
                break;
            case '>':
                token = NewToken(TokenType.GreaterThan, _ch);
                break;
            case ';':
                token = NewToken(TokenType.Semicolon, _ch);
                break;
            case '(':
                token = NewToken(TokenType.LeftParen, _ch);
                break;
            case ')':
                token = NewToken(TokenType.RightParen, _ch);
                break;
            case ',':
                token = NewToken(TokenType.Comma, _ch);
                break;
            case '{':
                token = NewToken(TokenType.LeftBrace, _ch);
                break;
            case '}':
                token = NewToken(TokenType.RightBrace, _ch);
                break;
            case '[':
                token = NewToken(TokenType.LeftBracket, _ch);
                break;
            case ']':
                token = NewToken(TokenType.RightBracket, _ch);
                break;
            case ':':
                token = NewToken(TokenType.Colon, _ch);
                break;
            case QuoteChar:
                if (!TryReadString(out var str))
                    token = NewToken(TokenType.Illegal, _ch);
                else
                    token = new Token(TokenType.String, str);
                break;
            case NullChar:
                token = new Token(TokenType.EndOfFile, "");
                break;
            default:
                if (IsLetter(_ch))
                {
                    var literal = ReadIdentifier();
                    token = new Token(LookupIdent(literal), literal);
                    return token;
                }

                if (IsDigit(_ch))
                {
                    token = new Token(TokenType.Integer, ReadNumber());
                    return token;
                }

                token = NewToken(TokenType.Illegal, _ch);

                break;
        }

        ReadChar();
        return token;
    }

    /// <summary>
    /// Creates a new Token instance representing the specified character and token type.
    /// </summary>
    /// <param name="type">The type of token to assign to the new Token instance.</param>
    /// <param name="ch">The character value to be represented by the token.</param>
    /// <returns>A Token object initialized with the specified type and character value.</returns>
    private static Token NewToken(TokenType type, char ch)
    {
        return new Token(type, ch.ToString());
    }

    /// <summary>
    /// Reads an identifier from the current position in the input and advances the position past the identifier.
    /// </summary>
    /// <remarks>An identifier is defined as a contiguous sequence of letter characters. The method
    /// does not consume non-letter characters and will return an empty string if the current character is not a
    /// letter.</remarks>
    /// <returns>A string containing the identifier found at the current position. Returns an empty string if no identifier
    /// is present.</returns>
    private string ReadIdentifier()
    {
        var position = _position;
        while (IsLetter(_ch))
            ReadChar();
        return _input[position.._position];
    }

    /// <summary>
    /// Determines whether the specified character is an ASCII letter or an underscore.
    /// </summary>
    /// <param name="ch">The character to evaluate.</param>
    /// <returns>true if the character is an uppercase or lowercase ASCII letter (A–Z, a–z) or an underscore (_); otherwise,
    /// false.</returns>
    private static bool IsLetter(char ch)
    {
        return ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
    }

    /// <summary>
    /// Determines the token type for the specified identifier based on known keywords.
    /// </summary>
    /// <param name="ident">The identifier to evaluate. If the identifier matches a known keyword, its corresponding token type is
    /// returned; otherwise, it is treated as a generic identifier.</param>
    /// <returns>The token type associated with the specified identifier. Returns the keyword's token type if recognized;
    /// otherwise, returns TokenType.Identifier.</returns>
    private static TokenType LookupIdent(string ident)
    {
        return Keywords.GetValueOrDefault(ident, TokenType.Identifier);
    }

    /// <summary>
    /// Advances the input position past any consecutive whitespace characters.
    /// </summary>
    /// <remarks>Whitespace characters include spaces, tabs, carriage returns, and line feeds. This
    /// method is typically used to ignore insignificant whitespace when processing input streams.</remarks>
    private void SkipWhitespace()
    {
        while (_ch is ' ' or '\t' or '\n' or '\r')
            ReadChar();
    }

    /// <summary>
    /// Determines whether the specified character is a decimal digit ('0' through '9').
    /// </summary>
    /// <param name="ch">The character to evaluate.</param>
    /// <returns>true if the character is a decimal digit; otherwise, false.</returns>
    private static bool IsDigit(char ch)
    {
        return ch is >= '0' and <= '9';
    }

    /// <summary>
    /// Extracts a sequence of consecutive digit characters from the current position in the input.
    /// </summary>
    /// <returns>A string containing the digits read from the input. Returns an empty string if no digits are found at the
    /// current position.</returns>
    private string ReadNumber()
    {
        var position = _position;
        while (IsDigit(_ch))
            ReadChar();
        return _input[position.._position];
    }

    /// <summary>
    /// Attempts to read a quoted string from the current input position.
    /// </summary>
    /// <remarks>The method reads characters until a closing quote character is encountered or the end
    /// of input is reached. If the end of input is reached before a closing quote, the method returns false and the
    /// output parameter is set to an empty string.
    /// Escape sequences are not supported by the language specification.</remarks>
    /// <param name="str">When this method returns, contains the string value that was read if successful; otherwise, contains an
    /// empty string.</param>
    /// <returns>true if a quoted string was successfully read; otherwise, false.</returns>
    private bool TryReadString(out string str)
    {
        str = string.Empty;
        var position = _position + 1;
        while (true)
        {
            ReadChar();
            if (_ch == QuoteChar)
                break;
            if (_ch == NullChar)
                return false;
        }

        str = _input[position.._position];
        return true;
    }

    /// <summary>
    /// Returns the next character in the input without advancing the read position.
    /// </summary>
    /// <returns>The next character in the input stream, or a sentinel value if the end of the input has been reached.</returns>
    private char PeekChar()
    {
        if (_readPosition >= _input.Length)
            return NullChar;
        return _input[_readPosition];
    }
}
