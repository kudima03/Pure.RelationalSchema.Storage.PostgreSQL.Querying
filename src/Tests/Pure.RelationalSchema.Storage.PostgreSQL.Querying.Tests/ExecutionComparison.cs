using System.Globalization;
using System.Runtime.CompilerServices;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Compares what a query returns over the stored tables of
/// <see cref="ExecutionFixture"/>, translated to one statement, with what it returns
/// over their enumerated rows in memory: the same elements, or the same type of
/// exception; or that the translation rejects it where the rows give an outcome.
/// </summary>
internal sealed record ExecutionComparison
{
    private const string Thrown = "throws ";

    private readonly ExecutionFixture _fixture;

    public ExecutionComparison(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The query returns the same elements, in any order.
    /// </summary>
    public void AssertSameElements<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        AssertSameOutcome(tables =>
            query(tables).AsEnumerable().Select(Canonical).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The query returns the same elements in the same order.
    /// </summary>
    public void AssertSameOrder<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        AssertSameOutcome(tables => query(tables).AsEnumerable().Select(Canonical));
    }

    /// <summary>
    /// The query returns the same value.
    /// </summary>
    public void AssertSameValue<T>(Func<ExecutionTables, T> query)
    {
        AssertSameOutcome(tables => [Canonical(query(tables))]);
    }

    /// <summary>
    /// The query over the stored tables returns, in any order, the elements the
    /// computation over the enumerated rows returns: for a query of PostgreSQL
    /// markers, which throw when called, so that the same query cannot run in
    /// memory.
    /// </summary>
    public void AssertSameElements<T>(
        Func<ExecutionTables, IQueryable<T>> stored,
        Func<ExecutionTables, IEnumerable<T>> enumerated
    )
    {
        AssertSameOutcome(
            tables =>
                stored(tables).AsEnumerable().Select(Canonical).Order(StringComparer.Ordinal),
            tables => enumerated(tables).Select(Canonical).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The query over the stored tables returns, in the same order, the elements the
    /// computation over the enumerated rows returns.
    /// </summary>
    public void AssertSameOrder<T>(
        Func<ExecutionTables, IQueryable<T>> stored,
        Func<ExecutionTables, IEnumerable<T>> enumerated
    )
    {
        AssertSameOutcome(
            tables => stored(tables).AsEnumerable().Select(Canonical),
            tables => enumerated(tables).Select(Canonical)
        );
    }

    /// <summary>
    /// The query over the stored tables returns the value the computation over the
    /// enumerated rows returns.
    /// </summary>
    public void AssertSameValue<T>(
        Func<ExecutionTables, T> stored,
        Func<ExecutionTables, T> enumerated
    )
    {
        AssertSameOutcome(
            tables => [Canonical(stored(tables))],
            tables => [Canonical(enumerated(tables))]
        );
    }

    /// <summary>
    /// The query over the stored tables throws <see cref="NotSupportedException"/>
    /// before any statement runs, where the one over the enumerated rows does not.
    /// </summary>
    public void AssertNotSupported<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        string[] expected = Outcome(() =>
            query(ExecutionTables.Enumerated(_fixture)).AsEnumerable().Select(Canonical)
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        Assert.NotEqual(Thrown + typeof(NotSupportedException).FullName, expected[0]);
        _ = Assert.Throws<NotSupportedException>(() =>
            query(ExecutionTables.Stored(_fixture, connection)).ToList()
        );
        Assert.Empty(connection.Statements);
    }

    /// <summary>
    /// The query over the stored tables throws <see cref="NotSupportedException"/>
    /// before any statement runs: for a query of PostgreSQL markers, which has no
    /// outcome in memory. Returns the exception.
    /// </summary>
    public NotSupportedException AssertRefused<T>(
        Func<ExecutionTables, IQueryable<T>> stored
    )
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            stored(ExecutionTables.Stored(_fixture, connection)).ToList()
        );
        Assert.Empty(connection.Statements);
        return exception;
    }

    /// <summary>
    /// The query returns elements over the enumerated rows, and fails over the
    /// stored tables with an exception of the type, in its one statement: a value
    /// PostgreSQL cannot hold, or a NULL no value is read from.
    /// </summary>
    public void AssertFails<TStored>(Func<ExecutionTables, IQueryable> query)
        where TStored : Exception
    {
        Assert.NotEmpty(Elements(query(ExecutionTables.Enumerated(_fixture))));
        AssertStoredFails<TStored>(query);
    }

    /// <summary>
    /// The query fails over the enumerated rows and over the stored tables, each
    /// with an exception of its type, the second in its one statement: a failure
    /// SQL does not raise as .NET does.
    /// </summary>
    public void AssertFails<TEnumerated, TStored>(Func<ExecutionTables, IQueryable> query)
        where TEnumerated : Exception
        where TStored : Exception
    {
        AssertFails<TEnumerated, TStored>(query, query);
    }

    /// <summary>
    /// The first computation fails over the stored tables and the second over the
    /// enumerated rows, each with an exception of its type, the first in its one
    /// statement.
    /// </summary>
    public void AssertFails<TEnumerated, TStored>(
        Func<ExecutionTables, IQueryable> stored,
        Func<ExecutionTables, IQueryable> enumerated
    )
        where TEnumerated : Exception
        where TStored : Exception
    {
        _ = Assert.Throws<TEnumerated>(() =>
            Elements(enumerated(ExecutionTables.Enumerated(_fixture)))
        );
        AssertStoredFails<TStored>(stored);
    }

    private void AssertStoredFails<TStored>(Func<ExecutionTables, IQueryable> query)
        where TStored : Exception
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        _ = Assert.Throws<TStored>(() =>
            Elements(query(ExecutionTables.Stored(_fixture, connection)))
        );
        _ = Assert.Single(connection.Statements);
    }

    private static List<object?> Elements(System.Collections.IEnumerable query)
    {
        return [.. query.Cast<object?>()];
    }

    private void AssertSameOutcome(Func<ExecutionTables, IEnumerable<string>> query)
    {
        AssertSameOutcome(query, query);
    }

    private void AssertSameOutcome(
        Func<ExecutionTables, IEnumerable<string>> stored,
        Func<ExecutionTables, IEnumerable<string>> enumerated
    )
    {
        string[] expected = Outcome(() =>
            enumerated(ExecutionTables.Enumerated(_fixture))
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(() =>
            stored(ExecutionTables.Stored(_fixture, connection))
        );

        Assert.Equal(expected, actual);
        if (expected is not [string only] || !only.StartsWith(Thrown, StringComparison.Ordinal))
        {
            _ = Assert.Single(connection.Statements);
        }
    }

    /// <summary>
    /// The elements the query returns, or the type of the exception it throws.
    /// </summary>
    private static string[] Outcome(Func<IEnumerable<string>> query)
    {
        try
        {
            return [.. query()];
        }
        catch (Exception exception)
        {
            return [Thrown + exception.GetType().FullName];
        }
    }

    /// <summary>
    /// The text of an element that tells null from empty text, names the type of a
    /// number and shows the cells of rows, in the order of their column names, and
    /// the items of tuples.
    /// </summary>
    private static string Canonical<T>(T value)
    {
        return value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            IRow row => "row("
                + string.Join(
                    ", ",
                    row.Cells.OrderBy(x => x.Key.Name.TextValue, StringComparer.Ordinal)
                        .Select(x => Canonical(x.Value.Value.TextValue))
                )
                + ")",
            ITuple tuple => "("
                + string.Join(
                    ", ",
                    Enumerable.Range(0, tuple.Length).Select(x => Canonical(tuple[x]))
                )
                + ")",
            DateTime moment => moment.ToString("O", CultureInfo.InvariantCulture),
            _ => value.GetType().Name
                + ":"
                + Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }
}
