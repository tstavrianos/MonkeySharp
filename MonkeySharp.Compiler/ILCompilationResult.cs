using System.Collections.Generic;

namespace MonkeySharp.Compiler;

/// <summary>
/// The result of compiling a MonkeySharp program with the IL compiler.
/// Check <see cref="IsValid"/> before calling <see cref="ILILSessionun"/>.
/// </summary>
public sealed class ILCompilationResult
{
    private readonly System.Func<MonkeyObject>? _compiled;

    /// <summary>Parse or compilation diagnostics. Empty when compilation succeeded.</summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>True when there are no diagnostics and the program was compiled successfully.</summary>
    public bool IsValid => Diagnostics.Count == 0 && _compiled != null;

    internal ILCompilationResult(
        System.Func<MonkeyObject>? compiled,
        IReadOnlyList<string> diagnostics
    )
    {
        _compiled = compiled;
        Diagnostics = diagnostics;
    }

    internal System.Func<MonkeyObject>? GetCompiledFunction()
    {
        return _compiled;
    }
}
