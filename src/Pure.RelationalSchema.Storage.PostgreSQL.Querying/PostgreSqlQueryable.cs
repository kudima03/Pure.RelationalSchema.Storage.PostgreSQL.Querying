using System.Collections;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record PostgreSqlQueryable<T> : IOrderedQueryable<T>
{
    public PostgreSqlQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator()
    {
        return PostgreSqlQueryProvider.Enumerate<T>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
