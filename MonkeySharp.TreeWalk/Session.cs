using System;
using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

public sealed class Session
{
    internal SymbolTable SymbolTable { get; }

    public Session()
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

    public CompilationResult Compile(string? source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new CompilationResult(null, new List<string>(parser.Errors));

        return new CompilationResult(program, []);
    }

    public ExecutionResult Run(CompilationResult compilationResult)
    {
        if (!compilationResult.IsValid)
        {
            var msg =
                compilationResult.Diagnostics.Count > 0
                    ? compilationResult.Diagnostics[0]
                    : "Cannot run an invalid compilation result.";
            return new ExecutionResult(MonkeyValue.Error(msg), msg);
        }

        if (compilationResult.ProgramNode is null)
            return new ExecutionResult(MonkeyValue.Null(), "Compilation result is invalid.");

        return Evaluate(compilationResult);
    }

    public ExecutionResult Evaluate(CompilationResult compilationResult)
    {
        ArgumentNullException.ThrowIfNull(compilationResult);

        if (!compilationResult.IsValid)
        {
            var message = string.Join("; ", compilationResult.Diagnostics);
            return new ExecutionResult(MonkeyValue.Error(message), message);
        }

        var value = TreeWalker.Evaluate(compilationResult, this);
        if (value.Kind == MonkeyValueKind.Error)
            return new ExecutionResult(value, value.ErrorMessage);
        return new ExecutionResult(value, null);
    }

    internal static SymbolTable CreateDefaultSymbolTable()
    {
        var symbolTable = new SymbolTable();
        Builtins.RegisterDefaults(symbolTable);
        return symbolTable;
    }
}
