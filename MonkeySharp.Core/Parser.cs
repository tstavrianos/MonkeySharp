using System;
using System.Collections.Generic;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;

namespace MonkeySharp.Core
{
    /// <summary>
    /// Parses a sequence of tokens produced by a lexer into an abstract syntax tree (AST) representing a program.
    /// </summary>
    /// <remarks>The Parser processes tokens from the provided lexer and constructs the corresponding AST
    /// nodes for statements and expressions. It collects any syntax errors encountered during parsing, which can be
    /// accessed via the Errors property. The Parser is not thread-safe and is intended for single-threaded
    /// use.</remarks>
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

        /// <summary>
        /// Retrieves the prefix parse function associated with the specified token type, if one is defined.
        /// </summary>
        /// <remarks>This method is typically used by the parser to determine how to parse expressions
        /// that begin with a specific token. If the token type is not recognized as a valid prefix, the method returns
        /// null, indicating that no prefix parse function is available.</remarks>
        /// <param name="type">The type of token for which to obtain the corresponding prefix parse function.</param>
        /// <returns>A delegate representing the prefix parse function for the given token type, or null if no such function
        /// exists.</returns>
        private PrefixParseFn GetPrefixParseFn(TokenType type)
        {
            return type switch
            {
                TokenType.Identifier => ParseIdentifier,
                TokenType.Integer => ParseIntegerLiteral,
                TokenType.Bang or TokenType.Minus => ParsePrefixExpression,
                TokenType.True or TokenType.False => ParseBooleanLiteral,
                TokenType.LeftParen => ParseGroupedExpression,
                TokenType.If => ParseIfExpression,
                TokenType.Function => ParseFunctionLiteral,
                TokenType.String => ParseStringLiteral,
                TokenType.LeftBracket => ParseArrayLiteral,
                TokenType.LeftBrace => ParseHashLiteral,
                _ => null
            };
        }

        /// <summary>
        /// Retrieves the infix parse function associated with the specified token type, if one exists.
        /// </summary>
        /// <param name="type">The type of token for which to obtain the corresponding infix parse function.</param>
        /// <returns>An <see cref="InfixParseFn"/> delegate that parses infix expressions for the given token type, or <see
        /// langword="null"/> if the token type does not have an associated infix parse function.</returns>
        private InfixParseFn GetInfixParseFn(TokenType type)
        {
            return type switch
            {
                TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Asterisk or
                TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or TokenType.GreaterThan
                    => ParseInfixExpression,
                TokenType.LeftParen => ParseCallExpression,
                TokenType.LeftBracket => ParseIndexExpression,
                _ => null
            };
        }

        private Token _current;
        private Token _peek;
        private readonly Lexer _lexer;
        private readonly List<string> _errors = [];

        /// <summary>
        /// Gets a read-only list of error messages associated with the current operation or object.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        public Parser(Lexer lexer)
        {
            _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
            NextToken();
            NextToken();
        }

        /// <summary>
        /// Parses a hash literal expression from the current token stream.
        /// </summary>
        /// <remarks>The method expects the current token to be at the start of a hash literal and parses
        /// key-value pairs until a closing brace is encountered. If the input does not conform to the expected hash
        /// literal syntax, the method returns <see langword="null"/>.</remarks>
        /// <returns>A <see cref="HashLiteral"/> representing the parsed hash literal, or <see langword="null"/> if parsing fails
        /// due to invalid syntax.</returns>
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

        /// <summary>
        /// Parses an index expression from the current token stream, using the specified left-hand expression as the
        /// target of the index operation.
        /// </summary>
        /// <param name="left">The expression to be used as the target of the index operation. Typically, represents the object or
        /// collection being indexed.</param>
        /// <returns>An IndexExpression representing the parsed index operation, or null if the expression is not valid or the
        /// expected closing bracket is missing.</returns>
        private IndexExpression ParseIndexExpression(Expression left)
        {
            var token = _current;
            NextToken();
            var index = ParseExpression(Precedence.Lowest);
            if (!ExpectPeek(TokenType.RightBracket)) return null;
            return new IndexExpression(token, left, index);
        }

        /// <summary>
        /// Parses an array literal expression from the current position in the token stream.
        /// </summary>
        /// <returns>An <see cref="ArrayLiteral"/> representing the parsed array literal expression.</returns>
        private ArrayLiteral ParseArrayLiteral()
        {
            var token = _current;
            var elements = ParseExpressionList(TokenType.RightBracket) ?? [];
            return new ArrayLiteral(token, elements);
        }

        /// <summary>
        /// Parses a comma-separated list of expressions until the specified end token is encountered.
        /// </summary>
        /// <remarks>The returned list will be empty if the end token is encountered immediately. If the
        /// end token is missing, the method returns null to indicate a parsing error.</remarks>
        /// <param name="end">The token type that marks the end of the expression list. Parsing continues until this token is found.</param>
        /// <returns>A list of parsed expressions, or null if the end token is not found as expected.</returns>
        private List<Expression> ParseExpressionList(TokenType end)
        {
            List<Expression> expressions = [];
            if (PeekTokenIs(end))
            {
                NextToken();
                return expressions;
            }

            NextToken();
            expressions.Add(ParseExpression(Precedence.Lowest));
            while (PeekTokenIs(TokenType.Comma))
            {
                NextToken();
                NextToken();
                expressions.Add(ParseExpression(Precedence.Lowest));
            }

            if (!ExpectPeek(end)) return null;
            return expressions;
        }

        /// <summary>
        /// Parses the current token as a string literal and returns a corresponding StringLiteral instance.
        /// </summary>
        /// <returns>A StringLiteral object representing the parsed string literal from the current token.</returns>
        private StringLiteral ParseStringLiteral()
        {
            return new StringLiteral(_current, _current.Literal);
        }

        /// <summary>
        /// Parses a function or method call expression using the specified left-hand expression as the callee.
        /// </summary>
        /// <param name="left">The expression representing the function or method being called. Typically the result of parsing an
        /// identifier or member access expression.</param>
        /// <returns>A CallExpression representing the parsed function or method call, including its arguments.</returns>
        private CallExpression ParseCallExpression(Expression left)
        {
            var token = _current;
            var arguments = ParseExpressionList(TokenType.RightParen) ?? [];
            return new CallExpression(token, left, arguments);
        }

        /// <summary>
        /// Parses a function literal from the current position in the token stream.
        /// </summary>
        /// <remarks>The method expects the function literal to begin with a left parenthesis for
        /// parameters and a left brace for the function body. If either is missing, the method returns <see
        /// langword="null"/>.</remarks>
        /// <returns>A <see cref="FunctionLiteral"/> representing the parsed function, or <see langword="null"/> if parsing fails
        /// due to invalid syntax.</returns>
        private FunctionLiteral ParseFunctionLiteral()
        {
            var token = _current;
            if (!ExpectPeek(TokenType.LeftParen)) return null;

            var parameters = ParseFunctionParameters() ?? [];
            if (!ExpectPeek(TokenType.LeftBrace)) return null;
            var body = ParseBlockStatement();
            return new FunctionLiteral(token, parameters, body);
        }

        /// <summary>
        /// Parses a comma-separated list of function parameter identifiers from the current token stream.
        /// </summary>
        /// <remarks>The method expects the current token stream to be positioned at the start of a
        /// function parameter list. If the parameter list is empty, an empty list is returned. If the closing
        /// parenthesis is missing, the method returns <see langword="null"/> to indicate a parsing error.</remarks>
        /// <returns>A list of <see cref="Identifier"/> objects representing the parsed function parameters, or <see
        /// langword="null"/> if the parameter list is not properly closed with a right parenthesis.</returns>
        private List<Identifier> ParseFunctionParameters()
        {
            List<Identifier> identifiers = [];
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

        /// <summary>
        /// Parses an 'if' expression from the current position in the token stream.
        /// </summary>
        /// <remarks>The method expects the 'if' expression to be properly formed, including parentheses
        /// around the condition and braces around the consequence and optional alternative blocks. If the expected
        /// tokens are not found in the correct order, the method returns <see langword="null"/>.</remarks>
        /// <returns>An <see cref="IfExpression"/> representing the parsed 'if' expression, or <see langword="null"/> if parsing
        /// fails due to invalid syntax.</returns>
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

        /// <summary>
        /// Parses a block statement from the current position in the token stream.
        /// </summary>
        /// <remarks>The method advances the token stream past the opening brace and collects statements
        /// until a closing brace or end of file is encountered. Statements that cannot be parsed are skipped.</remarks>
        /// <returns>A <see cref="BlockStatement"/> representing the parsed block, containing all statements found between the
        /// opening and closing braces.</returns>
        private BlockStatement ParseBlockStatement()
        {
            var token = _current;
            NextToken();
            List<Statement> statements = [];
            while (!CurTokenIs(TokenType.RightBrace) && !CurTokenIs(TokenType.EndOfFile))
            {
                var statement = ParseStatement();
                if (statement != null) statements.Add(statement);
                NextToken();
            }

            return new BlockStatement(token, statements);
        }

        /// <summary>
        /// Parses an expression enclosed in parentheses and returns the resulting expression tree.
        /// </summary>
        /// <returns>An <see cref="Expression"/> representing the parsed grouped expression, or <see langword="null"/> if the
        /// closing parenthesis is missing.</returns>
        private Expression ParseGroupedExpression()
        {
            NextToken();
            var expression = ParseExpression(Precedence.Lowest);
            if (!ExpectPeek(TokenType.RightParen)) return null;
            return expression;
        }

        /// <summary>
        /// Parses the current token as a boolean literal.
        /// </summary>
        /// <returns>A <see cref="BooleanLiteral"/> value representing the boolean literal indicated by the current token.</returns>
        private BooleanLiteral ParseBooleanLiteral()
        {
            return _current.Type == TokenType.True ? BooleanLiteral.True : BooleanLiteral.False;
        }

        /// <summary>
        /// Parses an infix expression using the specified left-hand side expression and the current token.
        /// </summary>
        /// <param name="left">The expression representing the left-hand side of the infix operation. Cannot be null.</param>
        /// <returns>An <see cref="InfixExpression"/> representing the parsed infix expression.</returns>
        private InfixExpression ParseInfixExpression(Expression left)
        {
            var token = _current;
            var precedence = CurPrecedence();
            NextToken();
            var right = ParseExpression(precedence);
            return new InfixExpression(token, left, token.Literal, right);
        }

        /// <summary>
        /// Parses the entire input and constructs a syntax tree representing the program.
        /// </summary>
        /// <remarks>This method processes tokens until the end of the input is reached. Each valid
        /// statement is added to the resulting program node. Invalid or unrecognized statements are skipped.</remarks>
        /// <returns>A <see cref="ProgramNode"/> containing all parsed statements in the program. The node will contain an empty
        /// statement list if the input is empty.</returns>
        public ProgramNode ParseProgram()
        {
            List<Statement> statements = [];

            while (_current.Type != TokenType.EndOfFile)
            {
                var statement = ParseStatement();
                if (statement != null) statements.Add(statement);
                NextToken();
            }

            return new ProgramNode(statements);
        }

        /// <summary>
        /// Parses the current token as an identifier and returns a corresponding Identifier object.
        /// </summary>
        /// <returns>An Identifier object representing the current token and its literal value.</returns>
        private Identifier ParseIdentifier()
        {
            return new Identifier(_current, _current.Literal);
        }

        /// <summary>
        /// Parses the current token as an integer literal.
        /// </summary>
        /// <returns>An <see cref="IntegerLiteral"/> representing the parsed integer value if successful; otherwise, <see
        /// langword="null"/> if the current token cannot be parsed as an integer.</returns>
        private IntegerLiteral ParseIntegerLiteral()
        {
            if (!long.TryParse(_current.Literal, out var value))
            {
                _errors.Add($"could not parse {_current.Literal} as integer");
                return null;
            }

            return new IntegerLiteral(_current, value);
        }

        /// <summary>
        /// Parses a prefix expression from the current token position in the input stream.
        /// </summary>
        /// <remarks>This method advances the current token and parses the right-hand side of the prefix
        /// expression using prefix precedence. It is typically used as part of an expression parsing routine in a
        /// recursive descent parser.</remarks>
        /// <returns>A <see cref="PrefixExpression"/> representing the parsed prefix expression.</returns>
        private PrefixExpression ParsePrefixExpression()
        {
            var token = _current;
            NextToken();
            var right = ParseExpression(Precedence.Prefix);
            return new PrefixExpression(token, token.Literal, right);
        }

        /// <summary>
        /// Parses the next statement from the input stream based on the current token.
        /// </summary>
        /// <remarks>This method determines the type of statement to parse by inspecting the current
        /// token. It supports parsing 'let' statements, 'return' statements, and general expression
        /// statements.</remarks>
        /// <returns>A <see cref="Statement"/> representing the parsed statement. The specific type of statement returned depends
        /// on the current token.</returns>
        private Statement ParseStatement() =>
            _current.Type switch
            {
                TokenType.Let => ParseLetStatement(),
                TokenType.Return => ParseReturnStatement(),
                _ => ParseExpressionStatement()
            };

        /// <summary>
        /// Parses the current input as an expression statement.
        /// </summary>
        /// <remarks>An expression statement consists of a single expression optionally followed by a
        /// semicolon. The parser advances past the semicolon if present.</remarks>
        /// <returns>An ExpressionStatement representing the parsed expression and its associated token.</returns>
        private ExpressionStatement ParseExpressionStatement()
        {
            var token = _current;
            var expression = ParseExpression(Precedence.Lowest);
            if (PeekTokenIs(TokenType.Semicolon)) NextToken();
            return new ExpressionStatement(token, expression);
        }

        /// <summary>
        /// Parses an expression from the current token stream using the specified precedence level.
        /// </summary>
        /// <remarks>This method advances the token stream as it parses. It uses prefix and infix parse
        /// functions to construct the expression tree according to operator precedence. If the current token does not
        /// have a registered prefix parse function, the method returns <see langword="null"/>.</remarks>
        /// <param name="precedence">The minimum precedence that the parsed expression must have. Determines how operators are grouped in the
        /// resulting expression tree.</param>
        /// <returns>An <see cref="Expression"/> representing the parsed expression, or <see langword="null"/> if no valid prefix
        /// parse function exists for the current token.</returns>
        private Expression ParseExpression(Precedence precedence)
        {
            var prefix = GetPrefixParseFn(_current.Type);
            if (prefix == null)
            {
                NoPrefixParseFnError(_current.Type);
                return null;
            }

            var leftExp = prefix();

            while (!PeekTokenIs(TokenType.Semicolon) && precedence < PeekPrecedence())
            {
                var infix = GetInfixParseFn(_peek.Type);
                if (infix == null) return leftExp;
                NextToken();
                leftExp = infix(leftExp);
            }

            return leftExp;
        }

        /// <summary>
        /// Parses a return statement from the current position in the token stream.
        /// </summary>
        /// <returns>A <see cref="ReturnStatement"/> representing the parsed return statement, including its return value
        /// expression.</returns>
        private ReturnStatement ParseReturnStatement()
        {
            var token = _current;
            NextToken();
            var returnValue = ParseExpression(Precedence.Lowest);
            if (PeekTokenIs(TokenType.Semicolon)) NextToken();
            return new ReturnStatement(token, returnValue);
        }

        /// <summary>
        /// Parses a 'let' statement from the current token stream and constructs a corresponding LetStatement node.
        /// </summary>
        /// <remarks>The method expects the current token to be the 'let' keyword and advances through the
        /// identifier, assignment, and expression tokens as required by the language grammar. If the statement is
        /// incomplete or malformed, the method returns null.</remarks>
        /// <returns>A LetStatement representing the parsed 'let' statement, or null if the statement is not valid or cannot be
        /// parsed.</returns>
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

        /// <summary>
        /// Retrieves the precedence level associated with the next token in the input stream.
        /// </summary>
        /// <returns>The precedence value for the next token if defined; otherwise, the lowest precedence.</returns>
        private Precedence PeekPrecedence()
        {
            return Precedences.GetValueOrDefault(_peek.Type, Precedence.Lowest);
        }

        /// <summary>
        /// Gets the precedence level of the current token type in the parsing context.
        /// </summary>
        /// <returns>A value of type <see cref="Precedence"/> representing the precedence of the current token. Returns <see
        /// cref="Precedence.Lowest"/> if the token type is not found in the precedence mapping.</returns>
        private Precedence CurPrecedence()
        {
            return Precedences.GetValueOrDefault(_current.Type, Precedence.Lowest);
        }

        /// <summary>
        /// Records an error indicating that no prefix parse function is defined for the specified token type.
        /// </summary>
        /// <param name="tokenType">The type of token for which a prefix parse function was not found.</param>
        private void NoPrefixParseFnError(TokenType tokenType)
        {
            _errors.Add($"no prefix parse function for {tokenType} found");
        }

        /// <summary>
        /// Advances to the next token in the input stream by updating the current and peek tokens.
        /// </summary>
        /// <remarks>Call this method to move forward in the token stream when processing or parsing
        /// input. This method updates the internal state to reflect the next available token.</remarks>
        private void NextToken()
        {
            _current = _peek;
            _peek = _lexer.NextToken();
        }

        /// <summary>
        /// Determines whether the current token is of the specified type.
        /// </summary>
        /// <param name="type">The token type to compare with the current token.</param>
        /// <returns>true if the current token matches the specified type; otherwise, false.</returns>
        private bool CurTokenIs(TokenType type)
        {
            return _current.Type == type;
        }

        /// <summary>
        /// Determines whether the next token in the input stream is of the specified type.
        /// </summary>
        /// <param name="type">The token type to compare against the next token in the input stream.</param>
        /// <returns>true if the next token is of the specified type; otherwise, false.</returns>
        private bool PeekTokenIs(TokenType type)
        {
            return _peek.Type == type;
        }

        /// <summary>
        /// Attempts to advance to the next token if the upcoming token matches the specified type.
        /// </summary>
        /// <param name="type">The expected type of the next token to match against.</param>
        /// <returns>true if the next token matches the specified type and the parser advances; otherwise, false.</returns>
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

        /// <summary>
        /// Records an error indicating that the next token does not match the expected type.
        /// </summary>
        /// <param name="type">The expected type of the next token.</param>
        private void PeekError(TokenType type)
        {
            var msg = $"expected next token to be {type}, got {_peek.Type} instead";
            _errors.Add(msg);
        }
    }
}