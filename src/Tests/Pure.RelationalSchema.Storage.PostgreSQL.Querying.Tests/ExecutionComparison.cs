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

    private void AssertSameOutcome(Func<ExecutionTables, IEnumerable<string>> query)
    {
        string[] expected = Outcome(() => query(ExecutionTables.Enumerated(_fixture)));

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(() =>
            query(ExecutionTables.Stored(_fixture, connection))
        );

        Assert.Equal(expected, actual);
        if (!expected[0].StartsWith(Thrown, StringComparison.Ordinal))
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
