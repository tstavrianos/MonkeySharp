namespace MonkeySharp.ReflectionEmit;

public sealed class ExecutionResult
{
    public bool Success => Error == null;
    public string? Error { get; }
    public MonkeyValue Value { get; }

    internal ExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
