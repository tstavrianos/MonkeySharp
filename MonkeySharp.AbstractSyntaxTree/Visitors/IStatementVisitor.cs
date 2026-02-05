using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree.Visitors;

/// <summary>
/// Defines a visitor interface for traversing and processing statement nodes in the abstract syntax tree.
/// Implements the Visitor pattern for statement-based AST nodes without returning a value.
/// </summary>
public interface IStatementVisitor
{
    /// <summary>
    /// Visits a let statement node that declares and optionally initializes a variable.
    /// </summary>
    /// <param name="letStatement">The let statement to visit.</param>
    void Visit(LetStatement letStatement);

    /// <summary>
    /// Visits a return statement node that returns a value from a function.
    /// </summary>
    /// <param name="returnStatement">The return statement to visit.</param>
    void Visit(ReturnStatement returnStatement);

    /// <summary>
    /// Visits an expression statement node that wraps an expression as a statement.
    /// </summary>
    /// <param name="expressionStatement">The expression statement to visit.</param>
    void Visit(ExpressionStatement expressionStatement);

    /// <summary>
    /// Visits a block statement node that contains a sequence of statements.
    /// </summary>
    /// <param name="blockStatement">The block statement to visit.</param>
    void Visit(BlockStatement blockStatement);
}

/// <summary>
/// Defines a generic visitor interface for traversing and processing statement nodes in the abstract syntax tree.
/// Implements the Visitor pattern for statement-based AST nodes with a return value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of value returned by the visitor methods.</typeparam>
public interface IStatementVisitor<out T>
{
    /// <summary>
    /// Visits a let statement node that declares and optionally initializes a variable.
    /// </summary>
    /// <param name="letStatement">The let statement to visit.</param>
    /// <returns>A value of type <typeparamref name="T"/> resulting from visiting the statement.</returns>
    T Visit(LetStatement letStatement);

    /// <summary>
    /// Visits a return statement node that returns a value from a function.
    /// </summary>
    /// <param name="returnStatement">The return statement to visit.</param>
    /// <returns>A value of type <typeparamref name="T"/> resulting from visiting the statement.</returns>
    T Visit(ReturnStatement returnStatement);

    /// <summary>
    /// Visits an expression statement node that wraps an expression as a statement.
    /// </summary>
    /// <param name="expressionStatement">The expression statement to visit.</param>
    /// <returns>A value of type <typeparamref name="T"/> resulting from visiting the statement.</returns>
    T Visit(ExpressionStatement expressionStatement);

    /// <summary>
    /// Visits a block statement node that contains a sequence of statements.
    /// </summary>
    /// <param name="blockStatement">The block statement to visit.</param>
    /// <returns>A value of type <typeparamref name="T"/> resulting from visiting the statement.</returns>
    T Visit(BlockStatement blockStatement);
}