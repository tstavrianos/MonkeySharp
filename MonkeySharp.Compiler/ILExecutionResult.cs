namespace MonkeySharp.Compiler;

public sealed class ILExecutionResult
{
    public bool Success => Error == null;
    public string? Error { get; }
    public MonkeyValue Value { get; }

    internal ILExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
