using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using MonkeySharp.Compiler;
using MonkeySharp.Interpreter;
using MonkeySharp.VirtualMachine;

namespace MonkeySharp.Benchmarks;

[MemoryDiagnoser]
[CPUUsageDiagnoser]
#if CHECK_CACHE_MISSES
[HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
[ReturnValueValidator(true)]
public class Benchmarks
{
    private const string Input =
        @"
let fibonacci = fn(x) {
    if (x == 0) {
        0
    } else {
        if (x == 1) {
            return 1;
        } else {
            fibonacci(x - 1) + fibonacci(x - 2);
        }
    }
};
fibonacci(20);";

    private static int fibonacci(int x)
    {
        if (x == 0)
        {
            return 0;
        }
        else
        {
            if (x == 1)
                return 1;
            else
                return fibonacci(x - 1) + fibonacci(x - 2);
        }
    }

    private ILCompilationResult _ilCompiled;
    private ILCompilerSession _ilCompilerSession;
    private ParseResult _evaluatorCompiled;
    private InterpreterSession _evaluatorSession;
    private VmCompilationResult _vmCompiled;
    private VmSession _vmSession;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _ilCompilerSession = new ILCompilerSession();
        _ilCompiled = _ilCompilerSession.Compile(Input);
        _evaluatorSession = new InterpreterSession();
        _evaluatorCompiled = _evaluatorSession.Compile(Input);
        _vmSession = new VmSession();
        _vmCompiled = _vmSession.Compile(Input);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.Native)]
    public long BenchmarkNative()
    {
        return fibonacci(20);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Evaluator)]
    public long BenchmarkEvaluator()
    {
        var result = _evaluatorSession.Evaluate(_evaluatorCompiled);
        if (!result.Success)
            return long.MinValue;
        if (result.Value.Kind != Interpreter.MonkeyValueKind.Integer)
            return long.MinValue;
        return result.Value.IntegerValue!.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Vm)]
    public long BenchmarkVm()
    {
        var result = _vmSession.Run(_vmCompiled);
        if (!result.Success)
            return long.MinValue;
        if (result.Value.Kind != VirtualMachine.MonkeyValueKind.Integer)
            return long.MinValue;
        return result.Value.IntegerValue!.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.IL)]
    public long BenchmarkIL()
    {
        var result = _ilCompilerSession.Run(_ilCompiled);
        if (!result.Success)
            return long.MinValue;
        if (result.Value is not MonkeyInteger integer)
            return long.MinValue;
        return integer.Value;
    }
}
