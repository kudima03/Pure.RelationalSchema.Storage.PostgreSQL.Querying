using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Inlines the trees of queries captured by closures, so that their tables are seen
/// as the roots they are: a member, a conversion or a call that reads no lambda
/// parameter and whose type is a query or a sequence of rows — a captured variable
/// of any type a table has, an indexer, a method returning a query — is evaluated,
/// and the tree of the query it holds takes its place.
/// <para>
/// Inlining stops where it makes no progress. A capture is kept, its arguments
/// inlined, when the tree it gives is again a capture of a member or method already
/// inlined since the last node that is not one, or when it gives a query already
/// given there: a query operator of its own (a method returning
/// <c>source.Provider.CreateQuery(Call(method, ...))</c>) gives back a call to
/// itself, which would be evaluated again without end, and so would two operators
/// that call each other; a capture kept that holds no query, such as a property
/// whose query is a read of the property, is refused with
/// <see cref="NotSupportedException"/>. A call that refuses this provider with
/// <see cref="NotSupportedException"/> when called (an operator for expression
/// trees only) is kept too. The translator then translates the call or refuses it.
/// </para>
/// </summary>
internal sealed class CapturedQueries : ExpressionVisitor
{
    private IReadOnlyList<(MemberInfo? Member, IQueryable Query)> _inlined = [];

    public override Expression? Visit(Expression? node)
    {
        if (
            node is null
            || !IsCapture(node)
            || !IsQuery(node.Type)
            || new FreeParameters().In(node)
            || Evaluated(node) is not IQueryable query
        )
        {
            return Descended(node);
        }
        if (_inlined.Any(x => ReferenceEquals(x.Query, query)))
        {
            return Kept(node);
        }
        IReadOnlyList<(MemberInfo? Member, IQueryable Query)> outer = _inlined;
        _inlined = [.. outer, (Member(node), query)];
        Expression inlined =
            Member(query.Expression) is MemberInfo member
            && _inlined.Any(x => x.Member == member)
                ? Kept(node)
                : Visit(query.Expression)!;
        _inlined = outer;
        return inlined;
    }

    /// <summary>
    /// A capture kept in the tree, its children inlined. One that holds no query
    /// would be evaluated again by the translator, as any closed expression is, and
    /// is refused instead.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The capture holds no query.
    /// </exception>
    private Expression Kept(Expression node)
    {
        Expression kept = Descended(node)!;
        return new QueryRoots().In(kept).Count > 0
            ? kept
            : throw new NotSupportedException(
                $"The query of '{node}' is read from itself and cannot be translated."
            );
    }

    /// <summary>
    /// Visits the children of a node, each the start of inlining of its own.
    /// </summary>
    private Expression? Descended(Expression? node)
    {
        IReadOnlyList<(MemberInfo? Member, IQueryable Query)> outer = _inlined;
        _inlined = [];
        Expression? visited = base.Visit(node);
        _inlined = outer;
        return visited;
    }

    private static IQueryable? Evaluated(Expression node)
    {
        try
        {
            return new ClosureValue(node).Value as IQueryable;
        }
        catch (NotSupportedException) when (node is MethodCallExpression)
        {
            return null;
        }
    }

    private static MemberInfo? Member(Expression node)
    {
        return node switch
        {
            MethodCallExpression call => call.Method,
            MemberExpression member => member.Member,
            _ => null,
        };
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
