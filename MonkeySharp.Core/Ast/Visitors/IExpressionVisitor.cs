using MonkeySharp.Core.Ast.Expressions;

namespace MonkeySharp.Core.Ast.Visitors;

/// <summary>
/// Defines a visitor interface for traversing and processing expression nodes in the abstract syntax tree.
/// Implements the Visitor pattern for type-safe operations on different expression types without return values.
/// </summary>
public interface IExpressionVisitor
{
    /// <summary>
    /// Visits an identifier expression node.
    /// </summary>
    /// <param name="identifier">The identifier expression to visit.</param>
    void Visit(Identifier identifier);

    /// <summary>
    /// Visits an integer literal expression node.
    /// </summary>
    /// <param name="integerLiteral">The integer literal expression to visit.</param>
    void Visit(IntegerLiteral integerLiteral);

    /// <summary>
    /// Visits a prefix expression node (e.g., -x, !x).
    /// </summary>
    /// <param name="prefixExpression">The prefix expression to visit.</param>
    void Visit(PrefixExpression prefixExpression);

    /// <summary>
    /// Visits an infix expression node (e.g., x + y, a == b).
    /// </summary>
    /// <param name="infixExpression">The infix expression to visit.</param>
    void Visit(InfixExpression infixExpression);

    /// <summary>
    /// Visits a boolean literal expression node.
    /// </summary>
    /// <param name="booleanLiteral">The boolean literal expression to visit.</param>
    void Visit(BooleanLiteral booleanLiteral);

    /// <summary>
    /// Visits an if-else conditional expression node.
    /// </summary>
    /// <param name="ifExpression">The 'if expression' to visit.</param>
    void Visit(IfExpression ifExpression);

    /// <summary>
    /// Visits a function literal expression node (lambda/anonymous function).
    /// </summary>
    /// <param name="functionLiteral">The function literal expression to visit.</param>
    void Visit(FunctionLiteral functionLiteral);

    /// <summary>
    /// Visits a function call expression node.
    /// </summary>
    /// <param name="callExpression">The call expression to visit.</param>
    void Visit(CallExpression callExpression);

    /// <summary>
    /// Visits a string literal expression node.
    /// </summary>
    /// <param name="stringLiteral">The string literal expression to visit.</param>
    void Visit(StringLiteral stringLiteral);

    /// <summary>
    /// Visits an array literal expression node.
    /// </summary>
    /// <param name="arrayLiteral">The array literal expression to visit.</param>
    void Visit(ArrayLiteral arrayLiteral);

    /// <summary>
    /// Visits an index access expression node (e.g., array[0], hash["key"]).
    /// </summary>
    /// <param name="indexExpression">The index expression to visit.</param>
    void Visit(IndexExpression indexExpression);

    /// <summary>
    /// Visits a hash literal expression node (dictionary/map).
    /// </summary>
    /// <param name="hashLiteral">The hash literal expression to visit.</param>
    void Visit(HashLiteral hashLiteral);
}

/// <summary>
/// Defines a generic visitor interface for traversing and processing expression nodes in the abstract syntax tree.
/// Implements the Visitor pattern for type-safe operations on different expression types with return values of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The return type of the visit operations.</typeparam>
public interface IExpressionVisitor<out T>
{
    /// <summary>
    /// Visits an identifier expression node.
    /// </summary>
    /// <param name="identifier">The identifier expression to visit.</param>
    /// <returns>The result of visiting the identifier expression.</returns>
    T Visit(Identifier identifier);

    /// <summary>
    /// Visits an integer literal expression node.
    /// </summary>
    /// <param name="integerLiteral">The integer literal expression to visit.</param>
    /// <returns>The result of visiting the integer literal expression.</returns>
    T Visit(IntegerLiteral integerLiteral);

    /// <summary>
    /// Visits a prefix expression node (e.g., -x, !x).
    /// </summary>
    /// <param name="prefixExpression">The prefix expression to visit.</param>
    /// <returns>The result of visiting the prefix expression.</returns>
    T Visit(PrefixExpression prefixExpression);

    /// <summary>
    /// Visits an infix expression node (e.g., x + y, a == b).
    /// </summary>
    /// <param name="infixExpression">The infix expression to visit.</param>
    /// <returns>The result of visiting the infix expression.</returns>
    T Visit(InfixExpression infixExpression);

    /// <summary>
    /// Visits a boolean literal expression node.
    /// </summary>
    /// <param name="booleanLiteral">The boolean literal expression to visit.</param>
    /// <returns>The result of visiting the boolean literal expression.</returns>
    T Visit(BooleanLiteral booleanLiteral);

    /// <summary>
    /// Visits an if-else conditional expression node.
    /// </summary>
    /// <param name="ifExpression">The 'if expression' to visit.</param>
    /// <returns>The result of visiting the 'if expression'.</returns>
    T Visit(IfExpression ifExpression);

    /// <summary>
    /// Visits a function literal expression node (lambda/anonymous function).
    /// </summary>
    /// <param name="functionLiteral">The function literal expression to visit.</param>
    /// <returns>The result of visiting the function literal expression.</returns>
    T Visit(FunctionLiteral functionLiteral);

    /// <summary>
    /// Visits a function call expression node.
    /// </summary>
    /// <param name="callExpression">The call expression to visit.</param>
    /// <returns>The result of visiting the call expression.</returns>
    T Visit(CallExpression callExpression);

    /// <summary>
    /// Visits a string literal expression node.
    /// </summary>
    /// <param name="stringLiteral">The string literal expression to visit.</param>
    /// <returns>The result of visiting the string literal expression.</returns>
    T Visit(StringLiteral stringLiteral);

    /// <summary>
    /// Visits an array literal expression node.
    /// </summary>
    /// <param name="arrayLiteral">The array literal expression to visit.</param>
    /// <returns>The result of visiting the array literal expression.</returns>
    T Visit(ArrayLiteral arrayLiteral);

    /// <summary>
    /// Visits an index access expression node (e.g., array[0], hash["key"]).
    /// </summary>
    /// <param name="indexExpression">The index expression to visit.</param>
    /// <returns>The result of visiting the index expression.</returns>
    T Visit(IndexExpression indexExpression);

    /// <summary>
    /// Visits a hash literal expression node (dictionary/map).
    /// </summary>
    /// <param name="hashLiteral">The hash literal expression to visit.</param>
    /// <returns>The result of visiting the hash literal expression.</returns>
    T Visit(HashLiteral hashLiteral);
}