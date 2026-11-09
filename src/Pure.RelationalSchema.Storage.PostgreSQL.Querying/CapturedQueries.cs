using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Inlines the trees of queries captured by closures, so that their tables are seen
/// as the roots they are: a member, a conversion or a call that reads no lambda
/// parameter and whose type is a query or a sequence of rows — a captured variable
/// of any type a table has, an indexer, a method returning a query — is evaluated,
/// and the tree of the query it holds takes its place.
/// </summary>
internal sealed class CapturedQueries : ExpressionVisitor
{
    public override Expression? Visit(Expression? node)
    {
        return node is not null
            && IsCapture(node)
            && IsQuery(node.Type)
            && !new FreeParameters().In(node)
            && new ClosureValue(node).Value is IQueryable query
            ? Visit(query.Expression)
            : base.Visit(node);
    }

    private static bool IsCapture(Expression node)
    {
        return node switch
        {
            MemberExpression => true,
            UnaryExpression { NodeType: ExpressionType.Convert } => true,
            MethodCallExpression call => call.Method.DeclaringType != typeof(Queryable)
                && call.Method.DeclaringType != typeof(Enumerable),
            _ => false,
        };
    }

    private static bool IsQuery(Type type)
    {
        return type.IsAssignableTo(typeof(IQueryable))
            || type.IsAssignableTo(typeof(IEnumerable<IRow>));
    }
}
