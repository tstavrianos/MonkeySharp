using System;
using System.Collections.Generic;

namespace MonkeySharp.ReflectionEmit;

/// <summary>
/// The result of compiling a MonkeySharp program with the IL compiler.
/// Check <see cref="IsValid"/> before calling <see cref="Session.Run"/>.
/// </summary>
public sealed class CompilationResult
{
    private readonly Func<MonkeyObject>? _compiled;

    /// <summary>Parse or compilation diagnostics. Empty when compilation succeeded.</summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>True when there are no diagnostics and the program was compiled successfully.</summary>
    public bool IsValid => Diagnostics.Count == 0 && _compiled != null;

    internal CompilationResult(Func<MonkeyObject>? compiled, IReadOnlyList<string> diagnostics)
    {
        _compiled = compiled;
        Diagnostics = diagnostics;
    }

    internal Func<MonkeyObject>? GetCompiledFunction()
    {
        return _compiled;
    }
}
