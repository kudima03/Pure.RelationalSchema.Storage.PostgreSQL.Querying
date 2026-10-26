using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Executes the LINQ composed over stored tables as one SQL statement (see
/// <see cref="TranslatedQuery"/>) and shapes its values as the result of the query:
/// a sequence, one element with the semantics of the element operator, or an
/// aggregate.
/// </summary>
internal sealed record PostgreSqlQueryProvider : IQueryProvider
{
    public IQueryable CreateQuery(Expression expression)
    {
        return new PostgreSqlUntypedQueryable(this, expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new PostgreSqlQueryable<TElement>(this, expression);
    }

    public object? Execute(Expression expression)
    {
        Expression query = Inlined(expression);
        if (query is ConstantExpression { Value: PostgreSqlStoredTableDataSet table })
        {
            return table.Rows;
        }
        TranslatedQuery translated = Translated(query);
        IReadOnlyList<object?> values = translated.Values();
        return translated.IsSequence ? values
            : translated.IsElement
                ? Element(values, translated.Name)
                    ?? new ClosureValue(Expression.Default(translated.ElementType)).Value
            : Aggregate(values, translated.ResultType);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        Expression query = Inlined(expression);
        if (query is ConstantExpression { Value: PostgreSqlStoredTableDataSet table })
        {
            return (TResult)table.Rows;
        }
        TranslatedQuery translated = Translated(query);
        IReadOnlyList<object?> values = translated.Values();
        return translated.IsSequence ? Sequence<TResult>(values, translated)
            : translated.IsElement ? Element(values.Cast<TResult>(), translated.Name)
            : (TResult)Aggregate(values, typeof(TResult))!;
    }

    /// <summary>
    /// The elements of a sequence query, as the queryable that enumerates it types
    /// them.
    /// </summary>
    public static IEnumerable<T> Enumerate<T>(Expression expression)
    {
        Expression query = Inlined(expression);
        return query is ConstantExpression { Value: PostgreSqlStoredTableDataSet table }
            ? table.Rows.Cast<T>()
            : Translated(query).Values().Cast<T>();
    }

    private static Expression Inlined(Expression expression)
    {
        return new CapturedQueries().Visit(expression);
    }

    /// <exception cref="NotSupportedException">
    /// The query is not a query operator over stored tables.
    /// </exception>
    private static TranslatedQuery Translated(Expression query)
    {
        return query is MethodCallExpression call
            && call.Method.DeclaringType == typeof(Queryable)
            ? new TranslatedQuery(call)
            : throw new NotSupportedException(
                "The query is not a query operator over stored tables, so it cannot "
                    + "be translated to SQL."
            );
    }

    /// <summary>
    /// The sequence of the values as the result type, which only a caller executing
    /// the query through the provider can name: the rows of a table or the values as
    /// they are. The queryable of any element type enumerates its own elements.
    /// </summary>
    private static TResult Sequence<TResult>(
        IReadOnlyList<object?> values,
        TranslatedQuery translated
    )
    {
        object sequence =
            translated.ElementType == typeof(IRow) ? values.Cast<IRow>() : values;
        return sequence is TResult typed
            ? typed
            : throw new NotSupportedException(
                $"The sequence of {translated.ElementType} returned by the query "
                    + $"operator {translated.Name} is read by enumerating the query."
            );
    }

    /// <summary>
    /// The element the operator returns from the row (or none) its SQL is limited to,
    /// with the semantics of the operator.
    /// </summary>
    private static T Element<T>(IEnumerable<T> values, string name)
    {
        return name switch
        {
            nameof(Queryable.First) or nameof(Queryable.Last) => values.First(),
            nameof(Queryable.FirstOrDefault) or nameof(Queryable.LastOrDefault) =>
                values.FirstOrDefault()!,
            nameof(Queryable.Single) => values.Single(),
            nameof(Queryable.SingleOrDefault) => values.SingleOrDefault()!,
            nameof(Queryable.ElementAt) => values.ElementAt(0),
            _ => values.ElementAtOrDefault(0)!,
        };
    }

    /// <summary>
    /// The one value of an aggregate; a NULL aggregate of a value type is that of no
    /// elements.
    /// </summary>
    private static object? Aggregate(IReadOnlyList<object?> values, Type type)
    {
        object? value = values[0];
        return
            value is null && type.IsValueType && Nullable.GetUnderlyingType(type) is null
            ? throw new InvalidOperationException("Sequence contains no elements")
            : value;
    }
}
