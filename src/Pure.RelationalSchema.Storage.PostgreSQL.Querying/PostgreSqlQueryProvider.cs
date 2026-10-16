using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record PostgreSqlQueryProvider : IQueryProvider
{
    private static readonly IQueryProvider InMemory = Array
        .Empty<object>()
        .AsQueryable()
        .Provider;

    public IQueryable CreateQuery(Expression expression)
    {
        return (IQueryable)
            Activator.CreateInstance(
                typeof(PostgreSqlQueryable<>).MakeGenericType(
                    expression.Type.GetGenericArguments()[0]
                ),
                this,
                expression
            )!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new PostgreSqlQueryable<TElement>(this, expression);
    }

    public object? Execute(Expression expression)
    {
        return InMemory.Execute(Split(expression));
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return InMemory.Execute<TResult>(Split(expression));
    }

    private static Expression Split(Expression expression)
    {
        return new InMemoryTables().Visit(
            new SchemaSubqueries().Visit(new CapturedQueries().Visit(expression))
        )!;
    }
}
