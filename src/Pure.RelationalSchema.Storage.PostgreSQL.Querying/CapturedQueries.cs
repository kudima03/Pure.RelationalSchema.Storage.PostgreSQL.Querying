using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Inlines the trees of queries captured by closures, so that their tables are seen
/// as the roots they are: a member, a conversion or a call that reads no lambda
/// parameter and whose type is a query or a sequence of rows — a captured variable
/// of any type a table has, an indexer, a method returning a query — is evaluated,
/// and the tree of the query it holds takes its place. A call is kept, its
/// arguments inlined, when inlining it makes no progress: a query operator of its
/// own (a method returning <c>source.Provider.CreateQuery(Call(method, ...))</c>)
/// gives back a call to the same method, which would be evaluated again without
/// end, and an operator that refuses this provider with
/// <see cref="NotSupportedException"/> when called gives nothing. The translator
/// then translates the call or refuses it.
/// </summary>
internal sealed class CapturedQueries : ExpressionVisitor
{
    public override Expression? Visit(Expression? node)
    {
        return node is not null
            && IsCapture(node)
            && IsQuery(node.Type)
            && !new FreeParameters().In(node)
            && Evaluated(node) is Expression inlined
            ? Visit(inlined)
            : base.Visit(node);
    }

    private static Expression? Evaluated(Expression node)
    {
        object? value;
        try
        {
            value = new ClosureValue(node).Value;
        }
        catch (NotSupportedException) when (node is MethodCallExpression)
        {
            return null;
        }
        return value is IQueryable query && !IsSameCall(node, query.Expression)
            ? query.Expression
            : null;
    }

    private static bool IsSameCall(Expression node, Expression inlined)
    {
        return node is MethodCallExpression call
            && inlined is MethodCallExpression result
            && result.Method == call.Method;
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
