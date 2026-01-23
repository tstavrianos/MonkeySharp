using System;
using System.Collections.Generic;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;

namespace MonkeySharp.Core
{
    public class Parser
    {
        private delegate Expression PrefixParseFn();

        private delegate Expression InfixParseFn(Expression left);

        private enum Precedence
        {
            Lowest = 1,
            Equals,
            LessGreater,
            Sum,
            Product,
            Prefix,
            Call
        }

        private Token _current;
        private Token _peek;
        private readonly Lexer _lexer;
        private readonly List<string> _errors = [];
        private static readonly Dictionary<TokenType, Func<Parser, PrefixParseFn>> _prefixParseFns = new();
        private static readonly Dictionary<TokenType, Func<Parser, InfixParseFn>> _infixParseFns = new();

        private static readonly Dictionary<TokenType, Precedence> Precedences = new()
        {
            {TokenType.Equal, Precedence.Equals},
            {TokenType.NotEqual, Precedence.Equals},
            {TokenType.LessThan, Precedence.LessGreater},
            {TokenType.GreaterThan, Precedence.LessGreater},
            {TokenType.Plus, Precedence.Sum},
            {TokenType.Minus, Precedence.Sum},
            {TokenType.Slash, Precedence.Product},
            {TokenType.Asterisk, Precedence.Product}
        };

        public IReadOnlyList<string> Errors => _errors;

        static Parser()
        {
            _prefixParseFns[TokenType.Identifier] = p => p.ParseIdentifier;
            _prefixParseFns[TokenType.Integer] = p => p.ParseIntegerLiteral;
            _prefixParseFns[TokenType.Bang] = p => p.ParsePrefixExpression;
            _prefixParseFns[TokenType.Minus] = p => p.ParsePrefixExpression;
            _prefixParseFns[TokenType.True] = p => p.ParseBoolean;
            _prefixParseFns[TokenType.False] = p => p.ParseBoolean;
            _prefixParseFns[TokenType.LeftParen] = p => p.ParseGroupedExpression;

            _infixParseFns[TokenType.Plus] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.Minus] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.Slash] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.Asterisk] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.Equal] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.NotEqual] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.LessThan] = p => p.ParseInfixExpression;
            _infixParseFns[TokenType.GreaterThan] = p => p.ParseInfixExpression;
        }

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            NextToken();
            NextToken();
        }

        private void NextToken()
        {
            _current = _peek;
            _peek = _lexer.NextToken();
        }

        public ProgramNode ParseProgram()
        {
            var statements = new List<Statement>();

            while (_current.Type != TokenType.Eof)
            {
                var statement = ParseStatement();

                if (statement != null)
                    statements.Add(statement);
                NextToken();
            }

            return new ProgramNode(statements);
        }

        private Statement ParseStatement()
        {
            switch (_current.Type)
            {
                case TokenType.Let:
                    return ParseLetStatement();
                case TokenType.Return:
                    return ParseReturnStatement();
                default:
                    return ParseExpressionStatement();
            }
        }

        private Statement ParseLetStatement()
        {
            var token = _current;
            if (!ExpectPeek(TokenType.Identifier)) return null;
            var name = new Identifier(_current, _current.Literal);
            if (!ExpectPeek(TokenType.Assign)) return null;

            while (_current.Type != TokenType.Semicolon)
                NextToken();

            return new LetStatement(token, name, null);
        }

        private Statement ParseReturnStatement()
        {
            var token = _current;
            NextToken();

            while (_current.Type != TokenType.Semicolon) NextToken();

            return new ReturnStatement(token, null);
        }

        private Statement ParseExpressionStatement()
        {
            var token = _current;
            var expression = ParseExpression(Precedence.Lowest);
            if (_peek.Type == TokenType.Semicolon)
                NextToken();
            return new ExpressionStatement(token, expression);
        }

        private Expression ParseExpression(Precedence precedence)
        {
            if (!_prefixParseFns.TryGetValue(_current.Type, out var prefixFn))
            {
                NoPrefixParseFnError(_current.Type);
                return null;
            }

            var prefix = prefixFn(this);
            var leftExp = prefix();

            while (_peek.Type != TokenType.Semicolon && precedence < PeekPrecedence())
            {
                if (!_infixParseFns.TryGetValue(_peek.Type, out var infixFn)) return leftExp;
                var infix = infixFn(this);
                NextToken();
                leftExp = infix(leftExp);
            }

            return leftExp;
        }

        private Expression ParseIdentifier()
        {
            return new Identifier(_current, _current.Literal);
        }

        private Expression ParseIntegerLiteral()
        {
            var token = _current;
            if (!long.TryParse(_current.Literal, out var value))
            {
                _errors.Add("Could not parse " + _current.Literal + " as integer.");
                return null;
            }

            return new IntegerLiteral(token, value);
        }

        private Expression ParsePrefixExpression()
        {
            var token = _current;

            var operatorLiteral = _current.Literal;

            NextToken();

            var right = ParseExpression(Precedence.Prefix);

            return new PrefixExpression(token, operatorLiteral, right);
        }

        private Expression ParseInfixExpression(Expression left)
        {
            var token = _current;
            var operatorLiteral = _current.Literal;

            var precedence = CurrentPrecedence();
            NextToken();
            var right = ParseExpression(precedence);
            return new InfixExpression(token, left, operatorLiteral, right);
        }

        private Expression ParseBoolean()
        {
            return _current.Type == TokenType.True ? BooleanLiteral.True : BooleanLiteral.False;
        }

        private Expression ParseGroupedExpression()
        {
            NextToken();
            var exp = ParseExpression(Precedence.Lowest);

            if (!ExpectPeek(TokenType.RightParen)) return null;

            return exp;
        }

        private bool ExpectPeek(TokenType type)
        {
            if (_peek.Type == type)
            {
                NextToken();
                return true;
            }

            PeekError(type);
            return false;
        }

        private Precedence PeekPrecedence()
        {
            return Precedences.GetValueOrDefault(_peek.Type, Precedence.Lowest);
        }

        private Precedence CurrentPrecedence()
        {
            return Precedences.GetValueOrDefault(_current.Type, Precedence.Lowest);
        }

        private void PeekError(TokenType tokenType)
        {
            _errors.Add($"Expected next token to be {tokenType}, got {_peek.Type} instead");
        }

        private void NoPrefixParseFnError(TokenType type)
        {
            _errors.Add($"No prefix parse function for {type.String()} found");
        }
    }
}