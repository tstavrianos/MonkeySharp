using System;
using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.Interpreter.Objects;

namespace MonkeySharp.Interpreter;

public sealed class InterpreterSession
{
    internal SymbolTable SymbolTable { get; }

    public InterpreterSession()
    {
        SymbolTable = CreateDefaultSymbolTable();
    }

    public void RegisterFunction(
        string name,
        int arity,
        Func<IReadOnlyList<MonkeyValue>, MonkeyValue> function
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name must be provided.", nameof(name));

        ArgumentNullException.ThrowIfNull(function);

        SymbolTable.Set(
            name,
            Builtins.CreateHostBuiltin(
                name,
                arity,
                args =>
                {
                    var publicArgs = new MonkeyValue[args.Count];
                    for (var i = 0; i < args.Count; i++)
                        publicArgs[i] = new MonkeyValue(args[i]);

                    return function(publicArgs).ToInternal();
                }
            )
        );
    }

    public ParseResult Compile(string source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new ParseResult(null, new List<string>(parser.Errors));

        return new ParseResult(program, []);
    }

    public InterpreterExecutionResult Run(ParseResult parseResult)
    {
        return Evaluate(parseResult);
    }

    public InterpreterExecutionResult Evaluate(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (!parseResult.IsValid)
        {
            var message = string.Join("; ", parseResult.Diagnostics);
            return new InterpreterExecutionResult(MonkeyValue.Error(message), message);
        }

        var value = Evaluator.Evaluate(parseResult, this);
        if (value.Kind == MonkeyValueKind.Error)
            return new InterpreterExecutionResult(value, value.ErrorMessage);
        return new InterpreterExecutionResult(value, null);
    }

    internal static SymbolTable CreateDefaultSymbolTable()
    {
        var symbolTable = new SymbolTable();
        Builtins.RegisterDefaults(symbolTable);
        return symbolTable;
    }
}
