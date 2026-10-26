using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

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
        return InMemory.Execute(Translated(expression));
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return InMemory.Execute<TResult>(Translated(expression));
    }

    /// <summary>
    /// The tree with its result read in SQL, which in-memory execution then returns.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The query cannot be translated to one SQL statement.
    /// </exception>
    private static Expression Translated(Expression expression)
    {
        return new CapturedQueries().Visit(expression) switch
        {
            ConstantExpression { Value: PostgreSqlStoredTableDataSet table } =>
                Expression.Constant(
                    new EnumerableQuery<IRow>(table.Rows),
                    expression.Type
                ),
            MethodCallExpression call
                when call.Method.DeclaringType == typeof(Queryable) => new SchemaQuery(
                call
            ).Result(),
            _ => throw new NotSupportedException(
                "The query is not a query operator over stored tables, so it cannot "
                    + "be translated to SQL."
            ),
        };
    }
}
