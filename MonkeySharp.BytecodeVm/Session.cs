using System;
using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Analyzers;
using MonkeySharp.BytecodeVm.Objects;

namespace MonkeySharp.BytecodeVm;

public sealed class Session
{
    private readonly Dictionary<
        string,
        (int arity, Func<IReadOnlyList<MonkeyValue>, MonkeyValue> function)
    > _customBuiltins = [];

    private readonly Value[] _globals = new Value[Vm.GlobalsSize];

    public void RegisterFunction(
        string name,
        int arity,
        Func<IReadOnlyList<MonkeyValue>, MonkeyValue> function
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name must be provided.", nameof(name));

        ArgumentNullException.ThrowIfNull(function);

        _customBuiltins[name] = (arity, function);
    }

    public CompilationResult Compile(string? source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new CompilationResult(null, new List<string>(parser.Errors), []);

        program = new Optimizer().Optimize(program);

        var builtinSignatures = GetBuiltinSignatures();
        var builtinNames = new List<string>(builtinSignatures.Count);
        foreach (var (name, _) in builtinSignatures)
            builtinNames.Add(name);

        var compiler = CreateCompiler();
        var err = compiler.Compile(program);
        if (!string.IsNullOrEmpty(err))
            return new CompilationResult(null, [err], builtinNames);

        return new CompilationResult(compiler.ByteCode(), [], builtinNames);
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

        if (compilationResult.ByteCode is null)
            return new ExecutionResult(MonkeyValue.Null(), "Compilation result is invalid.");

        var expectedBuiltins = compilationResult.BuiltinNames;
        var currentBuiltins = GetBuiltinSignatures();

        if (!expectedBuiltins.SequenceEqual(currentBuiltins.Select(x => x.name)))
            return new ExecutionResult(
                MonkeyValue.Null(),
                "Compilation result was created with a different builtin set than this session."
            );

        var vm = new Vm(compilationResult.ByteCode.Value, _globals, GetBuiltinValues());
        var error = vm.Run();
        return new ExecutionResult(new MonkeyValue(vm.LastPoppedStackElement), error);
    }

    internal BytecodeCompiler CreateCompiler()
    {
        return new BytecodeCompiler(GetBuiltinSignatures());
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

        var signatures = GetBuiltinSignatures().Select(x => (x.name, x.arity));
        var analyzer = new StaticAnalyzer();
        analyzer.Analyze(program, signatures, runSecurity: runSecurity);
        return new AnalysisResult(
            new List<string>(analyzer.AllErrors),
            new List<string>(analyzer.AllWarnings)
        );
    }

    internal IReadOnlyList<(string name, int arity)> GetBuiltinSignatures()
    {
        var signatures = new List<(string name, int arity)>(
            Builtins.Entries.Count + _customBuiltins.Count
        );

        foreach (var (name, arity, _) in Builtins.Entries)
            signatures.Add((name, arity));

        foreach (var (name, definition) in _customBuiltins)
            signatures.Add((name, definition.arity));

        return signatures;
    }

    private Value[] GetBuiltinValues()
    {
        var values = new Value[Builtins.Entries.Count + _customBuiltins.Count];
        var idx = 0;

        foreach (var (_, _, builtin) in Builtins.Entries)
            values[idx++] = builtin;

        foreach (var (name, definition) in _customBuiltins)
            values[idx++] = Builtins.CreateHostBuiltin(
                name,
                definition.arity,
                args =>
                {
                    var publicArgs = new MonkeyValue[args.Count];
                    for (var i = 0; i < args.Count; i++)
                        publicArgs[i] = new MonkeyValue(args[i]);

                    return definition.function(publicArgs).ToInternal();
                }
            );

        return values;
    }
}
