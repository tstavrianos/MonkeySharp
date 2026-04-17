using System;
using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Analyzers;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

public sealed class Session
{
    internal SymbolTable SymbolTable { get; }

    private readonly Dictionary<string, int> _customBuiltinArities = new();

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

        _customBuiltinArities[name] = arity;

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

        program = new Optimizer().Optimize(program);

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

    /// <summary>
    /// Parses <paramref name="source"/> and runs static analysis, returning errors and warnings
    /// without compiling or executing the program.
    /// </summary>
    /// <param name="source">Monkey source code to analyze.</param>
    /// <param name="runSecurity">Whether to include security analysis. Disabled by default due to
    /// false positives on implicit-return recursive functions.</param>
    public AnalysisResult Analyze(string? source, bool runSecurity = true)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new AnalysisResult(new List<string>(parser.Errors), []);

        var analyzer = new StaticAnalyzer();
        analyzer.Analyze(program, GetBuiltinSignaturesForAnalysis(), runSecurity: runSecurity);
        return new AnalysisResult(
            new List<string>(analyzer.AllErrors),
            new List<string>(analyzer.AllWarnings)
        );
    }

    private IEnumerable<(string, int)> GetBuiltinSignaturesForAnalysis()
    {
        foreach (var (name, arity, _) in Builtins.Entries)
            yield return (name, arity);
        foreach (var (name, arity) in _customBuiltinArities)
            yield return (name, arity);
    }
}
