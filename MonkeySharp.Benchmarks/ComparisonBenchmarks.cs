using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace MonkeySharp.Benchmarks;

[MemoryDiagnoser]
[CPUUsageDiagnoser]
#if CHECK_CACHE_MISSES
[HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
[ReturnValueValidator(true)]
#pragma warning disable CA1515
public class ComparisonBenchmarks
#pragma warning restore CA1515
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
            return 0;

        if (x == 1)
            return 1;
        return fibonacci(x - 1) + fibonacci(x - 2);
    }

    private ReflectionEmit.CompilationResult _reflectionEmitCompilationResult;
    private ReflectionEmit.Session _reflectionEmitSession;
    private TreeWalk.CompilationResult _treeWalkCompilationResult;
    private TreeWalk.Session _treeWalkSession;
    private BytecodeVm.CompilationResult _bytecodeVmCompilationResult;
    private BytecodeVm.Session _bytecodeVmSession;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _reflectionEmitSession = new ReflectionEmit.Session();
        _reflectionEmitCompilationResult = _reflectionEmitSession.Compile(Input);
        _treeWalkSession = new TreeWalk.Session();
        _treeWalkCompilationResult = _treeWalkSession.Compile(Input);
        _bytecodeVmSession = new BytecodeVm.Session();
        _bytecodeVmCompilationResult = _bytecodeVmSession.Compile(Input);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.Native)]
#pragma warning disable CA1822
    public long BenchmarkNative()
#pragma warning restore CA1822
    {
        return fibonacci(20);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.TreeWalk)]
    public long BenchmarkTreeWalk()
    {
        var result = _treeWalkSession.Evaluate(_treeWalkCompilationResult);
        if (!result.Success)
            return long.MinValue;
        if (result.Value.Kind != TreeWalk.MonkeyValueKind.Integer)
            return long.MinValue;
        return result.Value.IntegerValue!.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.BytecodeVm)]
    public long BenchmarkBytecodeVm()
    {
        var result = _bytecodeVmSession.Run(_bytecodeVmCompilationResult);
        if (!result.Success)
            return long.MinValue;
        if (result.Value.Kind != BytecodeVm.MonkeyValueKind.Integer)
            return long.MinValue;
        return result.Value.IntegerValue!.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ReflectionEmit)]
    public long BenchmarkReflectionEmit()
    {
        var result = _reflectionEmitSession.Run(_reflectionEmitCompilationResult);
        if (!result.Success)
            return long.MinValue;
        if (result.Value.Kind != ReflectionEmit.MonkeyValueKind.Integer)
            return long.MinValue;
        return result.Value.IntegerValue!.Value;
    }
}
