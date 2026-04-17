namespace MonkeySharp.TreeWalk;

/// <summary>
/// Represents the result of executing compiled Monkey code.
/// </summary>
public sealed class ExecutionResult
{
    /// <summary>
    /// Gets a value that indicates whether execution completed without an error.
    /// </summary>
    public bool Success => Error is null;

    /// <summary>
    /// Gets the execution error message when execution fails; otherwise, <see langword="null"/>.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the value produced by program execution.
    /// </summary>
    public MonkeyValue Value { get; }

    internal ExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
