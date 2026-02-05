using System.Collections.Generic;
using System.IO;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree.Serialization;

/// <summary>
/// Deserializes AST nodes from binary format created by AstBinarySerializer.
/// </summary>
public class AstBinaryDeserializer
{
    private readonly BinaryReader _reader;

    private AstBinaryDeserializer(BinaryReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Deserializes a program node from a binary stream.
    /// </summary>
    /// <param name="stream">The input stream to read from.</param>
    /// <returns>The deserialized program node.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream contains invalid data.</exception>
    public static ProgramNode Deserialize(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var deserializer = new AstBinaryDeserializer(reader);

        // Read and validate magic header
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != "MAST") throw new InvalidDataException("Invalid AST binary format: missing magic header");

        var version = reader.ReadByte();
        if (version != 1) throw new InvalidDataException($"Unsupported AST binary version: {version}");

        // Read statements
        var statementCount = reader.ReadInt32();
        var statements = new List<Statement>(statementCount);

        for (var i = 0; i < statementCount; i++) statements.Add(deserializer.ReadStatement());

        return new ProgramNode(statements);
    }


    private Token ReadToken()
    {
        var tokenType = (TokenType) _reader.ReadInt32();
        var literal = _reader.ReadString();
        return new Token(tokenType, literal);
    }

    private Statement ReadStatement()
    {
        var nodeType = (StatementType) _reader.ReadByte();

        return nodeType switch
        {
            StatementType.Let => ReadLetStatement(),
            StatementType.Return => ReadReturnStatement(),
            StatementType.Expression => ReadExpressionStatement(),
            StatementType.Block => ReadBlockStatement(),
            _ => throw new InvalidDataException($"Unknown statement node type: {nodeType}")
        };
    }

    private Expression ReadExpression()
    {
        var nodeType = (ExpressionType) _reader.ReadByte();

        return nodeType switch
        {
            ExpressionType.Identifier => ReadIdentifier(),
            ExpressionType.Integer => ReadIntegerLiteral(),
            ExpressionType.Boolean => ReadBooleanLiteral(),
            ExpressionType.String => ReadStringLiteral(),
            ExpressionType.Array => ReadArrayLiteral(),
            ExpressionType.Hash => ReadHashLiteral(),
            ExpressionType.Prefix => ReadPrefixExpression(),
            ExpressionType.Infix => ReadInfixExpression(),
            ExpressionType.If => ReadIfExpression(),
            ExpressionType.Function => ReadFunctionLiteral(),
            ExpressionType.Call => ReadCallExpression(),
            ExpressionType.Index => ReadIndexExpression(),
            _ => throw new InvalidDataException($"Unknown expression node type: {nodeType}")
        };
    }

    // Expression readers
    private Identifier ReadIdentifier()
    {
        var token = ReadToken();
        var value = _reader.ReadString();
        return new Identifier(token, value);
    }

    private IntegerLiteral ReadIntegerLiteral()
    {
        var token = ReadToken();
        var value = _reader.ReadInt64();
        return new IntegerLiteral(token, value);
    }

    private BooleanLiteral ReadBooleanLiteral()
    {
        //var token = ReadToken();
        var value = _reader.ReadBoolean();
        return value ? BooleanLiteral.True : BooleanLiteral.False;
    }

    private StringLiteral ReadStringLiteral()
    {
        var token = ReadToken();
        var value = _reader.ReadString();
        return new StringLiteral(token, value);
    }

    private ArrayLiteral ReadArrayLiteral()
    {
        var token = ReadToken();
        var count = _reader.ReadInt32();
        var elements = new List<Expression>(count);

        for (var i = 0; i < count; i++) elements.Add(ReadExpression());

        return new ArrayLiteral(token, elements);
    }

    private HashLiteral ReadHashLiteral()
    {
        var token = ReadToken();
        var count = _reader.ReadInt32();
        var pairs = new Dictionary<Expression, Expression>(count);

        for (var i = 0; i < count; i++)
        {
            var key = ReadExpression();
            var value = ReadExpression();
            pairs[key] = value;
        }

        return new HashLiteral(token, pairs);
    }

    private PrefixExpression ReadPrefixExpression()
    {
        var token = ReadToken();
        var op = _reader.ReadString();
        var right = ReadExpression();
        return new PrefixExpression(token, op, right);
    }

    private InfixExpression ReadInfixExpression()
    {
        var token = ReadToken();
        var left = ReadExpression();
        var op = _reader.ReadString();
        var right = ReadExpression();
        return new InfixExpression(token, left, op, right);
    }

    private IfExpression ReadIfExpression()
    {
        var token = ReadToken();
        var condition = ReadExpression();
        var consequence = ReadBlockStatement();

        var hasAlternative = _reader.ReadBoolean();
        var alternative = hasAlternative ? ReadBlockStatement() : null;

        return new IfExpression(token, condition, consequence, alternative);
    }

    private FunctionLiteral ReadFunctionLiteral()
    {
        var token = ReadToken();

        var paramCount = _reader.ReadInt32();
        var parameters = new List<Identifier>(paramCount);
        for (var i = 0; i < paramCount; i++) parameters.Add(ReadIdentifier());

        var body = ReadBlockStatement();
        return new FunctionLiteral(token, parameters, body);
    }

    private CallExpression ReadCallExpression()
    {
        var token = ReadToken();
        var function = ReadExpression();

        var argCount = _reader.ReadInt32();
        var arguments = new List<Expression>(argCount);
        for (var i = 0; i < argCount; i++) arguments.Add(ReadExpression());

        return new CallExpression(token, function, arguments);
    }

    private IndexExpression ReadIndexExpression()
    {
        var token = ReadToken();
        var left = ReadExpression();
        var index = ReadExpression();
        return new IndexExpression(token, left, index);
    }

    // Statement readers
    private LetStatement ReadLetStatement()
    {
        var token = ReadToken();
        var name = ReadIdentifier();
        var value = ReadExpression();
        return new LetStatement(token, name, value);
    }

    private ReturnStatement ReadReturnStatement()
    {
        var token = ReadToken();
        var returnValue = ReadExpression();
        return new ReturnStatement(token, returnValue);
    }

    private ExpressionStatement ReadExpressionStatement()
    {
        var token = ReadToken();
        var expression = ReadExpression();
        return new ExpressionStatement(token, expression);
    }

    private BlockStatement ReadBlockStatement()
    {
        var token = ReadToken();
        var count = _reader.ReadInt32();
        var statements = new List<Statement>(count);

        for (var i = 0; i < count; i++) statements.Add(ReadStatement());

        return new BlockStatement(token, statements);
    }
}