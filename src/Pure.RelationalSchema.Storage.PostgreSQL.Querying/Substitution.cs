using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Replaces the parameters of a tree by the expressions bound to them.
/// </summary>
internal sealed class Substitution(
    IReadOnlyDictionary<ParameterExpression, Expression> bindings
) : ExpressionVisitor
{
    private readonly IReadOnlyDictionary<ParameterExpression, Expression> _bindings =
        bindings;

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return _bindings.TryGetValue(node, out Expression? bound) ? bound : node;
    }
}
