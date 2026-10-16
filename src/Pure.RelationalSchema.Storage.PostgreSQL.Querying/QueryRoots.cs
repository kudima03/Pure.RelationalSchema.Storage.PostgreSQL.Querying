using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed class QueryRoots : ExpressionVisitor
{
    private readonly List<IQueryable> _roots = [];

    public IReadOnlyList<IQueryable> In(Expression expression)
    {
        _ = Visit(expression);
        return _roots;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable root)
        {
            _roots.Add(root);
        }
        return node;
    }
}
