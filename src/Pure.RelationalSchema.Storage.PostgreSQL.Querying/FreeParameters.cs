using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed class FreeParameters : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _declared = [];

    private bool _found;

    public bool In(Expression expression)
    {
        _ = Visit(expression);
        return _found;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _declared.UnionWith(node.Parameters);
        return base.VisitLambda(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _found |= !_declared.Contains(node);
        return node;
    }
}
