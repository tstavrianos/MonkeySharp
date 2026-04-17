using System;
using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.Compiler;

/// <summary>
/// Stateful session for the MonkeySharp IL compiler.
/// Register host functions once, then compile and run programs repeatedly.
/// </summary>
public sealed class ILCompilerSession
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
    /// Returns a <see cref="ILCompilationResult"/> whose <see cref="ILCompilationResult.IsValid"/>
    /// property indicates success.
    /// </summary>
    public ILCompilationResult Compile(string source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new ILCompilationResult(null, parser.Errors);

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

        var compiler = new ILCompiler(adapters);
        var compiled = compiler.Compile(program);
        return new ILCompilationResult(compiled, Array.Empty<string>());
    }

    /// <summary>
    /// Executes a previously compiled program and returns its result.
    /// </summary>
    public ILExecutionResult Run(ILCompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsValid)
        {
            var msg =
                result.Diagnostics.Count > 0
                    ? result.Diagnostics[0]
                    : "Cannot run an invalid compilation result.";
            return new ILExecutionResult(MonkeyValue.Error(msg), msg);
        }

        var value = result.GetCompiledFunction()!();
        if (value is MonkeyError err)
            return new ILExecutionResult(new MonkeyValue(value), err.Message);
        return new ILExecutionResult(new MonkeyValue(value), null);
    }
}
