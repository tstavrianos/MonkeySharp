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
            Call,
            Index
        }

        private Token _current;
        private Token _peek;
        private readonly Lexer _lexer;
        private readonly List<string> _errors = [];

        private static readonly Dictionary<TokenType, Func<Parser, PrefixParseFn>> PrefixParseFns = new();
        private static readonly Dictionary<TokenType, Func<Parser, InfixParseFn>> InfixParseFns = new();

        private static readonly Dictionary<TokenType, Precedence> Precedences = new()
        {
            {TokenType.Equal, Precedence.Equals},
            {TokenType.NotEqual, Precedence.Equals},
            {TokenType.LessThan, Precedence.LessGreater},
            {TokenType.GreaterThan, Precedence.LessGreater},
            {TokenType.Plus, Precedence.Sum},
            {TokenType.Minus, Precedence.Sum},
            {TokenType.Slash, Precedence.Product},
            {TokenType.Asterisk, Precedence.Product},
            {TokenType.LeftParen, Precedence.Call},
            {TokenType.LeftBracket, Precedence.Index}
        };

        public IReadOnlyList<string> Errors => _errors;

        static Parser()
        {
            PrefixParseFns[TokenType.Identifier] = (p) => p.ParseIdentifier;
            PrefixParseFns[TokenType.Integer] = (p) => p.ParseIntegerLiteral;
            PrefixParseFns[TokenType.Bang] = (p) => p.ParsePrefixExpression;
            PrefixParseFns[TokenType.Minus] = (p) => p.ParsePrefixExpression;
            PrefixParseFns[TokenType.True] = (p) => p.ParseBooleanLiteral;
            PrefixParseFns[TokenType.False] = (p) => p.ParseBooleanLiteral;
            PrefixParseFns[TokenType.LeftParen] = (p) => p.ParseGroupedExpression;
            PrefixParseFns[TokenType.If] = (p) => p.ParseIfExpression;
            PrefixParseFns[TokenType.Function] = (p) => p.ParseFunctionLiteral;
            PrefixParseFns[TokenType.String] = (p) => p.ParseStringLiteral;
            PrefixParseFns[TokenType.LeftBracket] = (p) => p.ParseArrayLiteral;
            PrefixParseFns[TokenType.LeftBrace] = (p) => p.ParseHashLiteral;

            InfixParseFns[TokenType.Plus] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.Minus] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.Slash] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.Asterisk] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.Equal] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.NotEqual] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.LessThan] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.GreaterThan] = (p) => p.ParseInfixExpression;
            InfixParseFns[TokenType.LeftParen] = (p) => p.ParseCallExpression;
            InfixParseFns[TokenType.LeftBracket] = (p) => p.ParseIndexExpression;
        }

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            NextToken();
            NextToken();
        }

        private HashLiteral ParseHashLiteral()
        {
            var token = _current;
            var pairs = new Dictionary<Expression, Expression>();
            while (!PeekTokenIs(TokenType.RightBrace))
            {
                NextToken();
                var key = ParseExpression(Precedence.Lowest);
                if (!ExpectPeek(TokenType.Colon)) return null;
                NextToken();
                var value = ParseExpression(Precedence.Lowest);
                pairs.Add(key, value);
                if (!PeekTokenIs(TokenType.RightBrace) && !ExpectPeek(TokenType.Comma)) return null;
            }

            if (!ExpectPeek(TokenType.RightBrace)) return null;
            return new HashLiteral(token, pairs);
        }

        private IndexExpression ParseIndexExpression(Expression left)
        {
            var token = _current;
            NextToken();
            var index = ParseExpression(Precedence.Lowest);
            if (!ExpectPeek(TokenType.RightBracket)) return null;
            return new IndexExpression(token, left, index);
        }

        private ArrayLiteral ParseArrayLiteral()
        {
            var token = _current;
            var elements = ParseExpressionList(TokenType.RightBracket);
            return new ArrayLiteral(token, elements);
        }

        private List<Expression> ParseExpressionList(TokenType end)
        {
            var args = new List<Expression>();
            if (PeekTokenIs(end))
            {
                NextToken();
                return args;
            }

            NextToken();
            args.Add(ParseExpression(Precedence.Lowest));
            while (PeekTokenIs(TokenType.Comma))
            {
                NextToken();
                NextToken();
                args.Add(ParseExpression(Precedence.Lowest));
            }

            if (!ExpectPeek(end)) return null;
            return args;
        }

        private StringLiteral ParseStringLiteral()
        {
            return new StringLiteral(_current, _current.Literal);
        }

        private CallExpression ParseCallExpression(Expression left)
        {
            var token = _current;
            var arguments = ParseExpressionList(TokenType.RightParen);
            return new CallExpression(token, left, arguments);
        }

        private FunctionLiteral ParseFunctionLiteral()
        {
            var token = _current;
            if (!ExpectPeek(TokenType.LeftParen)) return null;

            var parameters = ParseFunctionParameters();
            if (!ExpectPeek(TokenType.LeftBrace)) return null;
            var body = ParseBlockStatement();
            return new FunctionLiteral(token, parameters, body);
        }

        private List<Identifier> ParseFunctionParameters()
        {
            var identifiers = new List<Identifier>();
            if (PeekTokenIs(TokenType.RightParen))
            {
                NextToken();
                return identifiers;
            }

            NextToken();
            var ident = new Identifier(_current, _current.Literal);
            identifiers.Add(ident);
            while (PeekTokenIs(TokenType.Comma))
            {
                NextToken();
                NextToken();
                ident = new Identifier(_current, _current.Literal);
                identifiers.Add(ident);
            }

            if (!ExpectPeek(TokenType.RightParen)) return null;
            return identifiers;
        }

        private IfExpression ParseIfExpression()
        {
            var token = _current;
            if (!ExpectPeek(TokenType.LeftParen)) return null;
            NextToken();
            var condition = ParseExpression(Precedence.Lowest);
            if (!ExpectPeek(TokenType.RightParen)) return null;
            if (!ExpectPeek(TokenType.LeftBrace)) return null;
            var consequence = ParseBlockStatement();
            BlockStatement alternative = null;
            if (PeekTokenIs(TokenType.Else))
            {
                NextToken();
                if (!ExpectPeek(TokenType.LeftBrace)) return null;
                alternative = ParseBlockStatement();
            }

            return new IfExpression(token, condition, consequence, alternative);
        }

        private BlockStatement ParseBlockStatement()
        {
            var token = _current;
            NextToken();
            var statements = new List<Statement>();
            while (!CurTokenIs(TokenType.RightBrace) && !CurTokenIs(TokenType.EndOfFile))
            {
                var statement = ParseStatement();
                if (statement != null) statements.Add(statement);
                NextToken();
            }

            return new BlockStatement(token, statements);
        }

        private Expression ParseGroupedExpression()
        {
            NextToken();
            var exp = ParseExpression(Precedence.Lowest);
            if (!ExpectPeek(TokenType.RightParen)) return null;
            return exp;
        }

        private BooleanLiteral ParseBooleanLiteral()
        {
            var ret = _current.Type == TokenType.True ? BooleanLiteral.True : BooleanLiteral.False;
            return ret;
        }

        private InfixExpression ParseInfixExpression(Expression left)
        {
            var token = _current;
            var precedence = CurPrecedence();
            NextToken();
            var right = ParseExpression(precedence);
            return new InfixExpression(token, left, token.Literal, right);
        }

        public ProgramNode ParseProgram()
        {
            var statements = new List<Statement>();

            while (_current.Type != TokenType.EndOfFile)
            {
                var statement = ParseStatement();
                if (statement != null) statements.Add(statement);
                NextToken();
            }

            return new ProgramNode(statements);
        }

        private Identifier ParseIdentifier()
        {
            return new Identifier(_current, _current.Literal);
        }

        private IntegerLiteral ParseIntegerLiteral()
        {
            if (!long.TryParse(_current.Literal, out var value))
            {
                _errors.Add($"could not parse {_current.Literal} as integer");
                return null;
            }

            var lit = new IntegerLiteral(_current, value);
            return lit;
        }

        private PrefixExpression ParsePrefixExpression()
        {
            var token = _current;
            NextToken();
            var right = ParseExpression(Precedence.Prefix);
            return new PrefixExpression(token, token.Literal, right);
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

        private ExpressionStatement ParseExpressionStatement()
        {
            var token = _current;
            var expression = ParseExpression(Precedence.Lowest);
            if (PeekTokenIs(TokenType.Semicolon)) NextToken();
            return new ExpressionStatement(token, expression);
        }

        private Expression ParseExpression(Precedence precedence)
        {
            if (!PrefixParseFns.TryGetValue(_current.Type, out var prefixFn))
            {
                NoPrefixParseFnError(_current.Type);
                return null;
            }

            var prefix = prefixFn(this);
            var leftExp = prefix();

            while (!PeekTokenIs(TokenType.Semicolon) && precedence < PeekPrecedence())
            {
                if (!InfixParseFns.TryGetValue(_peek.Type, out var infixFn)) return leftExp;
                NextToken();
                var infix = infixFn(this);
                leftExp = infix(leftExp);
            }

            return leftExp;
        }

        private ReturnStatement ParseReturnStatement()
        {
            var token = _current;
            NextToken();
            var returnValue = ParseExpression(Precedence.Lowest);
            if (PeekTokenIs(TokenType.Semicolon)) NextToken();
            return new ReturnStatement(token, returnValue);
        }

        private LetStatement ParseLetStatement()
        {
            var token = _current;
            if (!ExpectPeek(TokenType.Identifier)) return null;
            var name = new Identifier(_current, _current.Literal);
            if (!ExpectPeek(TokenType.Assign)) return null;
            NextToken();
            var value = ParseExpression(Precedence.Lowest);
            if (value is FunctionLiteral functionLiteral) functionLiteral.Name = name.Value;

            if (PeekTokenIs(TokenType.Semicolon)) NextToken();

            return new LetStatement(token, name, value);
        }

        private Precedence PeekPrecedence()
        {
            return Precedences.GetValueOrDefault(_peek.Type, Precedence.Lowest);
        }

        private Precedence CurPrecedence()
        {
            return Precedences.GetValueOrDefault(_current.Type, Precedence.Lowest);
        }

        private void NoPrefixParseFnError(TokenType tokenType)
        {
            _errors.Add($"no prefix parse function for {tokenType} found");
        }

        private void NextToken()
        {
            _current = _peek;
            _peek = _lexer.NextToken();
        }

        private bool CurTokenIs(TokenType type)
        {
            return _current.Type == type;
        }

        private bool PeekTokenIs(TokenType type)
        {
            return _peek.Type == type;
        }

        private bool ExpectPeek(TokenType type)
        {
            if (PeekTokenIs(type))
            {
                NextToken();
                return true;
            }

            PeekError(type);

            return false;
        }

        private void PeekError(TokenType type)
        {
            var msg = $"expected next token to be {type}, got {_peek.Type} instead";
            _errors.Add(msg);
        }
    }
}