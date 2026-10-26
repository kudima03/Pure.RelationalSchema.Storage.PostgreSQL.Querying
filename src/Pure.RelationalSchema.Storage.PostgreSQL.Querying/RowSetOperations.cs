using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Rejects the set operations of a tree that combine rows of different tables: SQL
/// combines the columns of their tables by position, so the result would be neither
/// the rows of one table nor an error.
/// </summary>
internal sealed class RowSetOperations : ExpressionVisitor
{
    /// <exception cref="NotSupportedException">
    /// A set operation of the tree combines rows of different tables.
    /// </exception>
    public void Validate(Expression expression)
    {
        _ = Visit(expression);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        return QueryElement.IsSetOperation(node)
            && node.Type.GetGenericArguments()[0] == typeof(IRow)
            && !QueryElement.AreRowsOfOneTable(node.Arguments[0], node.Arguments[1])
            ? throw new NotSupportedException(
                $"The query operator {node.Method.Name} combines rows of different "
                    + "tables, so it cannot be translated to SQL."
            )
            : base.VisitMethodCall(node);
    }
}
