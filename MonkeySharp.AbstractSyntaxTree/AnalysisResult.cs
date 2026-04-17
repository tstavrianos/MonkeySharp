using System.Collections.Generic;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// The result of running static analysis on a parsed Monkey program.
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>
    /// Errors found during analysis. Programs with errors should not be run.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Warnings found during analysis. Programs with warnings can still be run.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>True when at least one error was found.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>True when at least one warning was found.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>True when analysis found no errors or warnings.</summary>
    public bool IsClean => !HasErrors && !HasWarnings;

    internal AnalysisResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }
}
