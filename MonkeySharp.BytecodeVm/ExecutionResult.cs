namespace MonkeySharp.BytecodeVm;

public sealed class ExecutionResult
{
    public bool Success => Error is null;
    public string? Error { get; }
    public MonkeyValue Value { get; }

    internal ExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
