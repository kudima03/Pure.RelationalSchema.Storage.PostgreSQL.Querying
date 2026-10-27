using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// A query operator whose tree must read stored tables of one connection only, or
/// one row of aggregates over them from a one-element in-memory root. The translator
/// turns it into SQL, which runs as one statement and whose rows are read as the
/// values of the result.
/// </summary>
internal sealed record TranslatedQuery
{
    private static readonly HashSet<string> Elements =
    [
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
        nameof(Queryable.Last),
        nameof(Queryable.LastOrDefault),
        nameof(Queryable.ElementAt),
        nameof(Queryable.ElementAtOrDefault),
    ];

    private readonly MethodCallExpression _call;

    public TranslatedQuery(MethodCallExpression call)
    {
        _call = call;
    }

    public string Name => _call.Method.Name;

    /// <summary>
    /// Whether the operator returns a sequence, otherwise an element or an aggregate.
    /// </summary>
    public bool IsSequence => _call.Type.IsAssignableTo(typeof(IQueryable));

    /// <summary>
    /// Whether the operator returns one element of its source, which the SQL limits
    /// to the one row the element is read from.
    /// </summary>
    public bool IsElement => Elements.Contains(Name);

    public Type ResultType => _call.Type;

    public Type ElementType =>
        IsSequence ? _call.Type.GetGenericArguments()[0] : _call.Type;

    /// <summary>
    /// The values the rows of the SQL result are read as: the elements of a sequence
    /// or of an element operator, or the one value of an aggregate.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The operator does not read stored tables of one connection only, combines
    /// rows of different tables, its result cannot be read, or the translator cannot
    /// turn it into SQL.
    /// </exception>
    public IReadOnlyList<object?> Values()
    {
        IReadOnlyList<IQueryable> roots = new QueryRoots().In(_call);
        PostgreSqlStoredTableDataSet[] tables =
        [
            .. roots.OfType<PostgreSqlStoredTableDataSet>(),
        ];
        bool oneConnection = tables.All(x =>
            ReferenceEquals(tables[0].Connection, x.Connection)
        );
        IQueryable[] others =
        [
            .. roots.Where(x => x is not PostgreSqlStoredTableDataSet),
        ];
        if (
            tables.Length == 0
            || !(others.Length == 0 || (oneConnection && IsOneRow(others)))
        )
        {
            throw new NotSupportedException(
                $"The query operator {Name} reads sources other than stored tables, "
                    + "so it cannot be translated to SQL."
            );
        }
        if (!oneConnection)
        {
            throw new NotSupportedException(
                $"The query operator {Name} reads tables of more than one connection, "
                    + "so it cannot be translated to SQL."
            );
        }
        new RowSetOperations().Validate(_call);
        ResultReader reader = new(ElementType, new QueryElement(_call).Value);
        return !reader.IsSupported
            ? throw new NotSupportedException(
                $"The query operator {Name} returns {ElementType}, which cannot be read "
                    + "from a SQL result."
            )
            : Values(tables[0].Connection, Sql(tables), reader);
    }

    /// <summary>
    /// Whether the sources that are not stored tables are the one-element in-memory
    /// root of one row of aggregates over the tables.
    /// </summary>
    private static bool IsOneRow(IReadOnlyList<IQueryable> others)
    {
        if (others is not [EnumerableQuery row])
        {
            return false;
        }
        IEnumerator elements = ((IEnumerable)row).GetEnumerator();
        return elements.MoveNext() && !elements.MoveNext();
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The element types of the roots are entity types built from "
            + "reflection objects, whose properties trimming cannot remove."
    )]
    private string Sql(IEnumerable<PostgreSqlStoredTableDataSet> tables)
    {
        try
        {
            return new PostgreSqlQuery(
                new EntityExpression(tables).Visit(_call)
            ).TextValue;
        }
        catch (Exception exception)
            when (exception
                    is NotSupportedException
                        or InvalidCastException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or ArgumentException
            )
        {
            throw new NotSupportedException(
                $"The query operator {Name} cannot be translated to SQL.",
                exception
            );
        }
    }

    private IReadOnlyList<object?> Values(
        IDbConnection connection,
        string sql,
        ResultReader reader
    )
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using IDataReader result = command.ExecuteReader();
        if (result.FieldCount != reader.Width)
        {
            throw new NotSupportedException(
                $"The SQL of the query operator {Name} returns {result.FieldCount} "
                    + $"columns where {reader.Width} are expected."
            );
        }
        Collection<object?> values = [];
        while (result.Read())
        {
            values.Add(reader.Read(result, 0));
        }
        return values;
    }
}
