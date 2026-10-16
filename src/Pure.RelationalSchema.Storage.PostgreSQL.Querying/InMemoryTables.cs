using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Turns the stored tables left in a tree into in-memory sources of their rows, so
/// that the operators over them run in memory instead of coming back to this
/// provider: in-memory execution keeps a queryable operator over a source that is
/// still <see cref="IQueryable"/>.
/// </summary>
internal sealed class InMemoryTables : ExpressionVisitor
{
    protected override Expression VisitConstant(ConstantExpression node)
    {
        return node.Value is PostgreSqlStoredTableDataSet table
            ? Expression.Constant(new EnumerableQuery<IRow>(table.Rows), node.Type)
            : node;
    }
}
