using System.Collections;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The query the provider creates for an expression whose element type is only known
/// at run time, as <c>Cast</c> and <c>OfType</c> compose over it.
/// </summary>
internal sealed record PostgreSqlUntypedQueryable : IQueryable
{
    public PostgreSqlUntypedQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public Type ElementType => Expression.Type.GetGenericArguments()[0];

    public Expression Expression { get; }

    public IQueryProvider Provider { get; }

    public IEnumerator GetEnumerator()
    {
        return ((IEnumerable)Provider.Execute(Expression)!).GetEnumerator();
    }
}
