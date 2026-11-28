using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Replaces the parameters of a tree by the expressions bound to them, but for
/// those a lambda of the tree declares, which stand for its own arguments there.
/// </summary>
internal sealed class Substitution(
    IReadOnlyDictionary<ParameterExpression, Expression> bindings
) : ExpressionVisitor
{
    private readonly IReadOnlyDictionary<ParameterExpression, Expression> _bindings =
        bindings;

    private readonly HashSet<ParameterExpression> _declared = [];

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        ParameterExpression[] declared = [.. node.Parameters.Where(_declared.Add)];
        Expression body = Visit(node.Body);
        _declared.ExceptWith(declared);
        return node.Update(body, node.Parameters);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return !_declared.Contains(node)
            && _bindings.TryGetValue(node, out Expression? bound)
            ? bound
            : node;
    }
}
