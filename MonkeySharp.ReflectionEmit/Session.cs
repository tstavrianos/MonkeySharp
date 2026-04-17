using System;
using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Analyzers;

namespace MonkeySharp.ReflectionEmit;

/// <summary>
/// Stateful session for the MonkeySharp IL compiler.
/// Register host functions once, then compile and run programs repeatedly.
/// </summary>
public sealed class Session
{
    private readonly Dictionary<
        string,
        (int arity, Func<IReadOnlyList<MonkeyValue>, MonkeyValue> fn)
    > _hostFunctions = new();

    /// <summary>
    /// Registers a host-side function that Monkey code can call by <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The identifier used in Monkey source code.</param>
    /// <param name="arity">The exact number of arguments the function accepts.</param>
    /// <param name="function">The callback invoked when Monkey code calls the function.</param>
    public void RegisterFunction(
        string name,
        int arity,
        Func<IReadOnlyList<MonkeyValue>, MonkeyValue> function
    )
    {
        ArgumentNullException.ThrowIfNull(function);
        _hostFunctions[name] = (arity, function);
    }

    /// <summary>
    /// Parses and compiles <paramref name="source"/> to a native .NET delegate.
    /// Returns a <see cref="CompilationResult"/> whose <see cref="CompilationResult.IsValid"/>
    /// property indicates success.
    /// </summary>
    public CompilationResult Compile(string? source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new CompilationResult(null, parser.Errors);

        program = new Optimizer().Optimize(program);

        // Adapt Func<IReadOnlyList<MonkeyValue>, MonkeyValue> -> Func<MonkeyObject[], MonkeyObject>
        // so ILCompiler can build wrapper types for host functions.
        var adapters = _hostFunctions.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var (a, fn) = kvp.Value;
                return (
                    a,
                    (Func<MonkeyObject[], MonkeyObject>)(
                        args =>
                        {
                            var publicArgs = new MonkeyValue[args.Length];
                            for (var i = 0; i < args.Length; i++)
                                publicArgs[i] = new MonkeyValue(args[i]);

                            return fn(publicArgs).ToInternal();
                        }
                    )
                );
            }
        );

        var compiler = new ReflectionEmitCompiler(adapters);
        var compiled = compiler.Compile(program);
        return new CompilationResult(compiled, Array.Empty<string>());
    }

    /// <summary>
    /// Executes a previously compiled program and returns its result.
    /// </summary>
    public ExecutionResult Run(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsValid)
        {
            var msg =
                result.Diagnostics.Count > 0
                    ? result.Diagnostics[0]
                    : "Cannot run an invalid compilation result.";
            return new ExecutionResult(MonkeyValue.Error(msg), msg);
        }

        if (result.GetCompiledFunction() is null)
            return new ExecutionResult(MonkeyValue.Null(), "Compilation result is invalid.");

        var value = result.GetCompiledFunction()!();
        if (value is MonkeyError err)
            return new ExecutionResult(new MonkeyValue(value), err.Message);
        return new ExecutionResult(new MonkeyValue(value), null);
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

        var signatures = GetBuiltinSignaturesForAnalysis();
        var analyzer = new StaticAnalyzer();
        analyzer.Analyze(program, signatures, runSecurity: runSecurity);
        return new AnalysisResult(
            new List<string>(analyzer.AllErrors),
            new List<string>(analyzer.AllWarnings)
        );
    }

    private IEnumerable<(string, int)> GetBuiltinSignaturesForAnalysis()
    {
        foreach (var (name, arity) in BuiltinFunctions.Signatures)
            yield return (name, arity);
        foreach (var (name, (arity, _)) in _hostFunctions)
            yield return (name, arity);
    }
}
