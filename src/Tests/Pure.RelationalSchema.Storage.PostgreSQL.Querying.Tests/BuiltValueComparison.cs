using System.Globalization;
using System.Runtime.CompilerServices;
using Npgsql;
using Xunit.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Compares a query over the stored tables of <see cref="ExecutionFixture"/> with the
/// same query over their enumerated rows where <see cref="ExecutionComparison"/>
/// cannot: with dates and times written to the tick and with their kind (it writes a
/// <see cref="TimeOnly"/> without its seconds), or where both fail, .NET with
/// <see cref="ArgumentOutOfRangeException"/> and PostgreSQL with an error of its own.
/// Writes the statements it runs to the output of the test.
/// </summary>
internal sealed record BuiltValueComparison
{
    private const string Thrown = "throws ";

    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public BuiltValueComparison(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// The query returns the same elements, in any order, in one statement.
    /// </summary>
    public void AssertSameElements<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        string[] expected = Outcome(() =>
            query(ExecutionTables.Enumerated(_fixture)).AsEnumerable().Select(Canonical)
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(() =>
            query(ExecutionTables.Stored(_fixture, connection))
                .AsEnumerable()
                .Select(Canonical)
        );

        Written(connection);
        Assert.Equal(expected, actual);
        if (
            expected.Length == 0
            || !expected[0].StartsWith(Thrown, StringComparison.Ordinal)
        )
        {
            _ = Assert.Single(connection.Statements);
        }
    }

    /// <summary>
    /// The query returns the same value in one statement.
    /// </summary>
    public void AssertSameValue<T>(Func<ExecutionTables, T> query)
    {
        AssertSameElements(tables => new[] { query(tables) }.AsQueryable());
    }

    /// <summary>
    /// The query throws <see cref="ArgumentOutOfRangeException"/> over the
    /// enumerated rows, and its one statement fails on the server over the stored
    /// tables instead of returning a value.
    /// </summary>
    public void AssertBothFail<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            query(ExecutionTables.Enumerated(_fixture)).ToList()
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        Exception? exception = Record.Exception(() =>
            query(ExecutionTables.Stored(_fixture, connection)).ToList()
        );

        Written(connection);
        _output.WriteLine(exception?.ToString() ?? "no exception");
        _ = Assert.IsType<PostgresException>(exception);
        _ = Assert.Single(connection.Statements);
    }

    private void Written(RecordingConnection connection)
    {
        foreach (string statement in connection.Statements)
        {
            _output.WriteLine(statement);
        }
    }

    /// <summary>
    /// The text of a value that tells null from empty text, names the type of a
    /// number, writes dates and times to the tick with their kind and shows the
    /// items of tuples.
    /// </summary>
    public static string Canonical<T>(T value)
    {
        return value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            ITuple tuple => "("
                + string.Join(
                    ", ",
                    Enumerable.Range(0, tuple.Length).Select(x => Canonical(tuple[x]))
                )
                + ")",
            DateTime moment => "DateTime:"
                + moment.ToString("O", CultureInfo.InvariantCulture),
            DateOnly day => "DateOnly:"
                + day.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => "TimeOnly:"
                + time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan span => "TimeSpan:"
                + span.ToString("c", CultureInfo.InvariantCulture),
            _ => value.GetType().Name
                + ":"
                + Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    private static string[] Outcome(Func<IEnumerable<string>> query)
    {
        try
        {
            return [.. query().Order(StringComparer.Ordinal)];
        }
        catch (Exception exception)
        {
            return [Thrown + exception.GetType().FullName];
        }
    }
}
