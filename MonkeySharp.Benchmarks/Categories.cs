using System.Data;

namespace MonkeySharp.Benchmarks;

internal static class Categories
{
    public const string Vm = "Vm";
    public const string Evaluator = "Evaluator";
    public const string ValueEvaluator = "ValueEvaluator";
    public const string VisitorEvaluator = "VisitorEvaluator";
    public const string StaticDispatch = "StaticDispatch";
    public const string Optimized = "Optimized";
    public const string IL = "IL";
    public const string Native = "Native";
}