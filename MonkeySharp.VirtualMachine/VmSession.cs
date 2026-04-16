using System;
using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.VirtualMachine.Objects;

namespace MonkeySharp.VirtualMachine;

public sealed class VmSession
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

    public VmCompilationResult Compile(string source)
    {
        var lexer = new Lexer(source ?? string.Empty);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();

        if (parser.Errors.Count > 0)
            return new VmCompilationResult(null, new List<string>(parser.Errors), []);

        var builtinSignatures = GetBuiltinSignatures();
        var builtinNames = new List<string>(builtinSignatures.Count);
        foreach (var (name, _) in builtinSignatures)
            builtinNames.Add(name);

        var compiler = CreateCompiler();
        var err = compiler.Compile(program);
        if (!string.IsNullOrEmpty(err))
            return new VmCompilationResult(null, [err], builtinNames);

        return new VmCompilationResult(compiler.ByteCode(), [], builtinNames);
    }

    public VmExecutionResult Run(VmCompilationResult vmCompilationResult)
    {
        if (!vmCompilationResult.IsValid || vmCompilationResult.ByteCode is null)
            return new VmExecutionResult(MonkeyValue.Null(), "CompiledObject is invalid.");

        var expectedBuiltins = vmCompilationResult.BuiltinNames;
        var currentBuiltins = GetBuiltinSignatures();

        if (!expectedBuiltins.SequenceEqual(currentBuiltins.Select(x => x.name)))
            return new VmExecutionResult(
                MonkeyValue.Null(),
                "CompiledObject was created with a different builtin set than this VmSession."
            );

        var vm = new Vm(vmCompilationResult.ByteCode.Value, _globals, GetBuiltinValues());
        var error = vm.Run();
        return new VmExecutionResult(new MonkeyValue(vm.LastPoppedStackElement), error);
    }

    internal Compiler CreateCompiler()
    {
        return new Compiler(GetBuiltinSignatures());
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
