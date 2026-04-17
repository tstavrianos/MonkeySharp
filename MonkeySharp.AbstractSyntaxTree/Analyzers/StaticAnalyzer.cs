using System.Collections.Generic;

namespace MonkeySharp.AbstractSyntaxTree.Analyzers;

/// <summary>
/// Comprehensive static analysis combining semantic, security, quality, and data flow analysis.
/// </summary>
internal class StaticAnalyzer
{
    private readonly SemanticAnalyzer _semanticAnalyzer = new();
    private readonly SecurityAnalyzer _securityAnalyzer = new();
    private readonly CodeQualityAnalyzer _qualityAnalyzer = new();
    private readonly DataFlowAnalyzer _dataFlowAnalyzer = new();

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    public IReadOnlyList<string> AllErrors => _errors;
    public IReadOnlyList<string> AllWarnings => _warnings;

    /// <summary>
    /// Performs comprehensive static analysis on the given AST node.
    /// </summary>
    /// <param name="node">The AST node to analyze</param>
    /// <param name="builtinNamesAndArguments">Names and argument counts of built-in functions</param>
    /// <param name="runSecurity">Whether to run security analysis</param>
    /// <param name="runQuality">Whether to run code quality analysis</param>
    /// <param name="runDataFlow">Whether to run data flow analysis</param>
    /// <returns>True if no errors were found (warnings are acceptable)</returns>
    public void Analyze(
        Node node,
        IEnumerable<(string, int)>? builtinNamesAndArguments = null,
        bool runSecurity = true,
        bool runQuality = true,
        bool runDataFlow = true
    )
    {
        _errors.Clear();
        _warnings.Clear();

        // 1. Always run semantic analysis first (it's foundational)
        var semanticSuccess = _semanticAnalyzer.Analyze(node, builtinNamesAndArguments);
        _errors.AddRange(_semanticAnalyzer.Errors);
        _warnings.AddRange(_semanticAnalyzer.Warnings);

        // Only continue with other analyses if semantic analysis passed
        if (!semanticSuccess)
            return;
        // 2. Security Analysis
        if (runSecurity)
        {
            _securityAnalyzer.Analyze(node);
            _warnings.AddRange(_securityAnalyzer.Warnings);
        }

        // 3. Code Quality Analysis
        if (runQuality)
        {
            _qualityAnalyzer.Analyze(node);
            _warnings.AddRange(_qualityAnalyzer.Warnings);
        }

        // 4. Data Flow Analysis
        if (!runDataFlow)
            return;
        _dataFlowAnalyzer.Analyze(node);
        _errors.AddRange(_dataFlowAnalyzer.Errors);
        _warnings.AddRange(_dataFlowAnalyzer.Warnings);
    }

    /// <summary>
    /// Gets a formatted report of all analysis results.
    /// </summary>
    public string GetReport()
    {
        var lines = new List<string>();

        if (AllErrors.Count > 0)
        {
            lines.Add($"=== Errors ({AllErrors.Count}) ===");
            foreach (var error in AllErrors)
                lines.Add($"  ❌ {error}");
            lines.Add("");
        }

        if (AllWarnings.Count > 0)
        {
            lines.Add($"=== Warnings ({AllWarnings.Count}) ===");
            foreach (var warning in AllWarnings)
                lines.Add($"  ⚠️  {warning}");
            lines.Add("");
        }

        if (AllErrors.Count == 0 && AllWarnings.Count == 0)
            lines.Add("✅ No issues found");

        return string.Join("\n", lines);
    }
}
