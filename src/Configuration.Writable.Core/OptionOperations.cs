using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Configuration.Writable;

/// <summary>
/// Represents a collection of operations to be performed on configuration properties.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public sealed class OptionOperations<T> : IOptionOperator<T>
    where T : class
{
    private readonly List<string> _keysToDelete = [];

    /// <summary>
    /// Gets the list of property paths that have been marked for deletion.
    /// </summary>
    public IReadOnlyList<string> KeysToDelete => _keysToDelete.AsReadOnly();

    /// <inheritdoc />
    public void DeleteKey(Expression<Func<T, object?>> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var path = GetPropertyPath(selector);
        if (!_keysToDelete.Contains(path))
        {
            _keysToDelete.Add(path);
        }
    }

    /// <summary>
    /// Gets whether any operations have been registered.
    /// </summary>
    public bool HasOperations => _keysToDelete.Count > 0;

    /// <summary>
    /// Converts an expression tree to a configuration property path.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <returns>The property path in configuration format (e.g., "Parent:Child").</returns>
    private static string GetPropertyPath(Expression<Func<T, object?>> expression)
    {
        var parts = new List<string>();
        var current = expression.Body;

        // Unwrap Convert expression if present (happens with value types)
        if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            current = unary.Operand;
        }

        // Traverse the expression tree to build the path
        while (current != null)
        {
            if (current is MemberExpression memberExpr)
            {
                parts.Insert(0, memberExpr.Member.Name);
                current = memberExpr.Expression;
            }
            else if (current is ParameterExpression)
            {
                // Reached the root parameter
                break;
            }
            else
            {
                throw new ArgumentException(
                    $"Expression must be a simple property accessor. Unsupported expression type: {current.GetType().Name}",
                    nameof(expression)
                );
            }
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException(
                "Expression must select at least one property.",
                nameof(expression)
            );
        }

        return string.Join(":", parts);
    }
}
