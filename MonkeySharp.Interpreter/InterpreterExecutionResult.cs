namespace MonkeySharp.Interpreter;

public sealed class InterpreterExecutionResult
{
    public bool Success => Error is null;
    public string? Error { get; }
    public MonkeyValue Value { get; }

    internal InterpreterExecutionResult(MonkeyValue value, string? error)
    {
        Value = value;
        Error = error;
    }
}
