using MonkeySharp.Core.Ast.Visitors;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.Ast;

/// <summary>
/// Represents an abstract base class for all expression nodes in the Abstract Syntax Tree (AST).
/// Expressions are constructs that evaluate to a value.
/// </summary>
/// <param name="token">The token associated with this expression node.</param>
public abstract class Expression(Token token) : Node(token)
{
    /// <summary>
    /// Thread-safe cache for compiled visitor dispatch delegates.
    /// Maps a tuple of (expression type, visitor type) to an optimized dispatch function.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type ExprType, Type VisitorType), Delegate> _dispatchCache = new();

    /// <summary>
    /// Accepts a visitor that processes this expression node without returning a value.
    /// This method implements the Visitor pattern for AST traversal.
    /// </summary>
    /// <param name="visitor">The expression visitor to accept.</param>
    public abstract void Accept(IExpressionVisitor visitor);

    /// <summary>
    /// Accepts a visitor that processes this expression node and returns a value of type <typeparamref name="T"/>.
    /// This method implements the Visitor pattern for AST traversal with a return value.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);

    /// <summary>
    /// Optimized cached visitor dispatch that avoids virtual method calls.
    /// This method uses compiled expressions to create fast delegates that directly invoke
    /// the appropriate visitor method, eliminating the overhead of virtual dispatch.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the visitor.</typeparam>
    /// <param name="visitor">The expression visitor to accept.</param>
    /// <returns>A value of type <typeparamref name="T"/> produced by the visitor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T AcceptCached<T>(IExpressionVisitor<T> visitor)
    {
        var expressionType = GetType();
        var visitorType = visitor.GetType();
        var cacheKey = (expressionType, visitorType);

        if (!_dispatchCache.TryGetValue(cacheKey, out var cachedDelegate))
        {
            cachedDelegate = BuildDispatcher<T>(expressionType, visitorType);
            _dispatchCache.TryAdd(cacheKey, cachedDelegate);
        }

        var dispatcher = (Func<Expression, IExpressionVisitor<T>, T>) cachedDelegate;
        return dispatcher(this, visitor);
    }

    /// <summary>
    /// Builds a compiled delegate that directly dispatches to the appropriate visitor method
    /// without going through virtual method calls. This uses Expression Trees to generate
    /// optimized IL code at runtime.
    /// </summary>
    /// <typeparam name="T">The return type of the visitor method.</typeparam>
    /// <param name="expressionType">The concrete type of the expression node.</param>
    /// <param name="visitorType">The concrete type of the visitor.</param>
    /// <returns>A compiled delegate that efficiently dispatches to the visitor method.</returns>
    private static Func<Expression, IExpressionVisitor<T>, T> BuildDispatcher<T>(Type expressionType, Type visitorType)
    {
        // Find the Visit method on the visitor that accepts this expression type
        var visitMethod = typeof(IExpressionVisitor<T>).GetMethod("Visit", new[] {expressionType});

        if (visitMethod == null)
            // Fallback to standard dispatch if method not found
            return (expr, visitor) => expr.Accept(visitor);

        // Build expression: (expr, visitor) => visitor.Visit((ConcreteExpressionType)expr)
        var exprParam = System.Linq.Expressions.Expression.Parameter(typeof(Expression), "expr");
        var visitorParam = System.Linq.Expressions.Expression.Parameter(typeof(IExpressionVisitor<T>), "visitor");

        // Cast expression to concrete type
        var castedExpr = System.Linq.Expressions.Expression.Convert(exprParam, expressionType);

        // Call visitor.Visit(castedExpr)
        var visitCall = System.Linq.Expressions.Expression.Call(visitorParam, visitMethod, castedExpr);

        // Compile to delegate
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<Expression, IExpressionVisitor<T>, T>>(
            visitCall,
            exprParam,
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