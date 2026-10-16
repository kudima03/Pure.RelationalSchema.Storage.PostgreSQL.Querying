using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Inlines the trees of queries captured by closures, so that their tables are seen
/// as the roots they are.
/// </summary>
internal sealed class CapturedQueries : ExpressionVisitor
{
    protected override Expression VisitMember(MemberExpression node)
    {
        return
            node.Type.IsAssignableTo(typeof(IQueryable))
            && !new FreeParameters().In(node)
            && new ClosureValue(node).Value is IQueryable query
            && node.Type.IsAssignableFrom(query.Expression.Type)
            ? Visit(query.Expression)
            : base.VisitMember(node);
    }
}
