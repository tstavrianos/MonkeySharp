#pragma warning disable CA1852 // Seal internal types

using System.Globalization;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using MonkeySharp.Benchmarks;

CultureInfo cultureInfo = new("en-US");

CultureInfo.CurrentCulture = cultureInfo;
CultureInfo.CurrentUICulture = cultureInfo;
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var benchmark = BenchmarkSwitcher.FromTypes([typeof(ComparisonBenchmarks)]);

IConfig configuration = DefaultConfig
    .Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator)
    .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));

if (args.Length > 0)
    benchmark.Run(args, configuration);
else
    benchmark.RunAll(configuration);
