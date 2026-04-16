namespace MonkeySharp.VirtualMachine;

public sealed class VmExecutionResult
{
    public bool Success => Error is null;
    public string? Error { get; }
    public MonkeyValue Value { get; }

    internal VmExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
