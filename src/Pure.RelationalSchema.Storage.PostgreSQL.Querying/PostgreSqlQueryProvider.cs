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
            : translated.IsElement ? Element(values, translated)
            : values[0];
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
        if (translated.IsSequence)
        {
            return Sequence<TResult>(values, translated);
        }
        object? value = translated.IsElement ? Element(values, translated) : values[0];
        return (TResult)value!;
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
        return new CapturedQueries().Visit(expression)!;
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
    /// with the semantics of the operator: the exception of LINQ to Objects where it
    /// has no element to return, otherwise the default value of the operator.
    /// </summary>
    private static object? Element(IReadOnlyList<object?> values, TranslatedQuery query)
    {
        return query.Name switch
        {
            nameof(Queryable.First) or nameof(Queryable.Last) => values.Count == 0
                ? throw new InvalidOperationException("Sequence contains no elements")
                : values[0],
            nameof(Queryable.Single) => values.Single(),
            nameof(Queryable.SingleOrDefault) => values.Count == 0
                ? query.Default
                : values.Single(),
            nameof(Queryable.ElementAt) => values.Count == 0
                ? throw new ArgumentOutOfRangeException(
                    nameof(values),
                    "The query has no element at the index."
                )
                : values[0],
            nameof(Queryable.MinBy) or nameof(Queryable.MaxBy) => values.Count > 0 ? values[0]
                : query.HasNullableElement ? null
                : throw new InvalidOperationException("Sequence contains no elements"),
            _ => values.Count == 0 ? query.Default : values[0],
        };
    }
}
