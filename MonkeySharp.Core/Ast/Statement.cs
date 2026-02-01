using MonkeySharp.Core.Ast.Visitors;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.Ast;

/// <summary>
/// Represents an abstract base class for all statement nodes in the Abstract Syntax Tree (AST).
/// Statements are executable units that perform actions but do not produce values.
/// This class implements the Visitor pattern to allow double dispatch for statement processing.
/// </summary>
/// <param name="token">The token associated with this statement node.</param>
public abstract class Statement(Token token) : Node(token)
{
    /// <summary>
    /// Thread-safe cache for compiled visitor dispatch delegates.
    /// Maps a tuple of (statement type, visitor type) to an optimized dispatch function.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type StmtType, Type VisitorType), Delegate> _dispatchCache = new();

    /// <summary>
    /// Accepts a visitor for processing this statement node without returning a value.
    /// This method implements the Visitor pattern to enable double dispatch for statement traversal.
    /// </summary>
    /// <param name="visitor">The statement visitor that will process this node.</param>
    public abstract void Accept(IStatementVisitor visitor);

    /// <summary>
    /// Accepts a visitor for processing this statement node and returns a value of type <typeparamref name="T"/>.
    /// This method implements the Visitor pattern to enable double dispatch for statement traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The statement visitor that will process this node and return a value.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    public abstract T Accept<T>(IStatementVisitor<T> visitor);

    /// <summary>
    /// Optimized cached visitor dispatch that avoids virtual method calls.
    /// This method uses compiled expressions to create fast delegates that directly invoke
    /// the appropriate visitor method, eliminating the overhead of virtual dispatch.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The statement visitor to accept.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T AcceptCached<T>(IStatementVisitor<T> visitor)
    {
        var statementType = GetType();
        var visitorType = visitor.GetType();
        var cacheKey = (statementType, visitorType);

        if (!_dispatchCache.TryGetValue(cacheKey, out var cachedDelegate))
        {
            cachedDelegate = BuildDispatcher<T>(statementType, visitorType);
            _dispatchCache.TryAdd(cacheKey, cachedDelegate);
        }

        var dispatcher = (Func<Statement, IStatementVisitor<T>, T>) cachedDelegate;
        return dispatcher(this, visitor);
    }

    /// <summary>
    /// Builds a compiled delegate that directly dispatches to the appropriate visitor method
    /// without going through virtual method calls. This uses Expression Trees to generate
    /// optimized IL code at runtime.
    /// </summary>
    /// <typeparam name="T">The return type of the visitor method.</typeparam>
    /// <param name="statementType">The concrete type of the statement node.</param>
    /// <param name="visitorType">The concrete type of the visitor.</param>
    /// <returns>A compiled delegate that efficiently dispatches to the visitor method.</returns>
    private static Func<Statement, IStatementVisitor<T>, T> BuildDispatcher<T>(Type statementType, Type visitorType)
    {
        // Find the Visit method on the visitor that accepts this statement type
        var visitMethod = typeof(IStatementVisitor<T>).GetMethod("Visit", new[] {statementType});

        if (visitMethod == null)
            // Fallback to standard dispatch if method not found
            return (stmt, visitor) => stmt.Accept(visitor);

        // Build expression: (stmt, visitor) => visitor.Visit((ConcreteStatementType)stmt)
        var stmtParam = System.Linq.Expressions.Expression.Parameter(typeof(Statement), "stmt");
        var visitorParam = System.Linq.Expressions.Expression.Parameter(typeof(IStatementVisitor<T>), "visitor");

        // Cast statement to concrete type
        var castedStmt = System.Linq.Expressions.Expression.Convert(stmtParam, statementType);

        // Call visitor.Visit(castedStmt)
        var visitCall = System.Linq.Expressions.Expression.Call(visitorParam, visitMethod, castedStmt);

        // Compile to delegate
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<Statement, IStatementVisitor<T>, T>>(
            visitCall,
            stmtParam,
            visitorParam
        );

        return lambda.Compile();
    }

    /// <summary>
    /// Clears the dispatch cache. Useful for testing or when memory pressure is high.
    /// </summary>
    public static void ClearDispatchCache()
    {
        _dispatchCache.Clear();
    }

    /// <summary>
    /// Gets the current number of cached dispatch delegates.
    /// </summary>
    public static int GetCacheSize()
    {
        return _dispatchCache.Count;
    }
}