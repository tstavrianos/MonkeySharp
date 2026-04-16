namespace MonkeySharp.Compiler;

public sealed class ILExecutionResult
{
    public bool Success => Error == null;
    public string? Error { get; }
    public MonkeyObject? Value { get; }

    internal ILExecutionResult(MonkeyObject? value, string? error)
    {
        Value = value;
        Error = error;
    }
}
