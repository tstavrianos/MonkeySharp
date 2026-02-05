using System.IO;
using System.Text;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree.Serialization;

/// <summary>
/// Visitor that serializes AST nodes to binary format.
/// Implements both expression and statement visitors to traverse the entire AST.
/// </summary>
public class AstBinarySerializer : IExpressionVisitor, IStatementVisitor
{
    private readonly BinaryWriter _writer;

    private AstBinarySerializer(BinaryWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Serializes a program node to a binary stream.
    /// </summary>
    /// <param name="program">The program node to serialize.</param>
    /// <param name="stream">The output stream to write to.</param>
    public static void Serialize(ProgramNode program, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        var serializer = new AstBinarySerializer(writer);

        // Write magic header for validation
        writer.Write(Encoding.ASCII.GetBytes("MAST")); // MonkeySharp AST
        writer.Write((byte) 1); // Version

        // Write statements count
        writer.Write(program.Statements.Count);

        foreach (var statement in program.Statements) statement.Accept(serializer);
    }

    private void WriteToken(Token token)
    {
        _writer.Write((int) token.Type);
        _writer.Write(token.Literal ?? string.Empty);
    }

    // Expression visitors
    public void Visit(Identifier identifier)
    {
        _writer.Write((byte) identifier.ExpressionType);
        WriteToken(identifier.Token);
        _writer.Write(identifier.Value);
    }

    public void Visit(IntegerLiteral integerLiteral)
    {
        _writer.Write((byte) integerLiteral.ExpressionType);
        WriteToken(integerLiteral.Token);
        _writer.Write(integerLiteral.Value);
    }

    public void Visit(BooleanLiteral booleanLiteral)
    {
        _writer.Write((byte) booleanLiteral.ExpressionType);
        //WriteToken(booleanLiteral.Token);
        _writer.Write(booleanLiteral.Value);
    }

    public void Visit(StringLiteral stringLiteral)
    {
        _writer.Write((byte) stringLiteral.ExpressionType);
        WriteToken(stringLiteral.Token);
        _writer.Write(stringLiteral.Value);
    }

    public void Visit(ArrayLiteral arrayLiteral)
    {
        _writer.Write((byte) arrayLiteral.ExpressionType);
        WriteToken(arrayLiteral.Token);
        _writer.Write(arrayLiteral.Elements.Count);
        foreach (var element in arrayLiteral.Elements) element.Accept(this);
    }

    public void Visit(HashLiteral hashLiteral)
    {
        _writer.Write((byte) hashLiteral.ExpressionType);
        WriteToken(hashLiteral.Token);
        _writer.Write(hashLiteral.Pairs.Count);
        foreach (var (key, value) in hashLiteral.Pairs)
        {
            key.Accept(this);
            value.Accept(this);
        }
    }

    public void Visit(PrefixExpression prefixExpression)
    {
        _writer.Write((byte) prefixExpression.ExpressionType);
        WriteToken(prefixExpression.Token);
        _writer.Write(prefixExpression.Operator);
        prefixExpression.Right.Accept(this);
    }

    public void Visit(InfixExpression infixExpression)
    {
        _writer.Write((byte) infixExpression.ExpressionType);
        WriteToken(infixExpression.Token);
        infixExpression.Left.Accept(this);
        _writer.Write(infixExpression.Operator);
        infixExpression.Right.Accept(this);
    }

    public void Visit(IfExpression ifExpression)
    {
        _writer.Write((byte) ifExpression.ExpressionType);
        WriteToken(ifExpression.Token);
        ifExpression.Condition.Accept(this);
        ifExpression.Consequence.Accept(this);

        // Write whether alternative exists
        var hasAlternative = ifExpression.Alternative != null;
        _writer.Write(hasAlternative);
        if (hasAlternative) ifExpression.Alternative!.Accept(this);
    }

    public void Visit(FunctionLiteral functionLiteral)
    {
        _writer.Write((byte) functionLiteral.ExpressionType);
        WriteToken(functionLiteral.Token);

        // Write parameters
        _writer.Write(functionLiteral.Parameters.Count);
        foreach (var parameter in functionLiteral.Parameters) parameter.Accept(this);

        functionLiteral.Body.Accept(this);
    }

    public void Visit(CallExpression callExpression)
    {
        _writer.Write((byte) callExpression.ExpressionType);
        WriteToken(callExpression.Token);
        callExpression.Function.Accept(this);

        _writer.Write(callExpression.Arguments.Count);
        foreach (var argument in callExpression.Arguments) argument.Accept(this);
    }

    public void Visit(IndexExpression indexExpression)
    {
        _writer.Write((byte) indexExpression.ExpressionType);
        WriteToken(indexExpression.Token);
        indexExpression.Left.Accept(this);
        indexExpression.Index.Accept(this);
    }

    // Statement visitors
    public void Visit(LetStatement letStatement)
    {
        _writer.Write((byte) letStatement.StatementType);
        WriteToken(letStatement.Token);
        letStatement.Name.Accept(this);
        letStatement.Value.Accept(this);
    }

    public void Visit(ReturnStatement returnStatement)
    {
        _writer.Write((byte) returnStatement.StatementType);
        WriteToken(returnStatement.Token);
        returnStatement.ReturnValue.Accept(this);
    }

    public void Visit(ExpressionStatement expressionStatement)
    {
        _writer.Write((byte) expressionStatement.StatementType);
        WriteToken(expressionStatement.Token);
        expressionStatement.Expression.Accept(this);
    }

    public void Visit(BlockStatement blockStatement)
    {
        _writer.Write((byte) blockStatement.StatementType);
        WriteToken(blockStatement.Token);
        _writer.Write(blockStatement.Statements.Count);
        foreach (var statement in blockStatement.Statements) statement.Accept(this);
    }
}