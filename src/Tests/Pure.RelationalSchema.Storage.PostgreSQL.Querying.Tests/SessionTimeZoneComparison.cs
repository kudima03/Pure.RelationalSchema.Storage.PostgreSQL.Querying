namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Compares what a query returns over the stored tables of a
/// <see cref="SessionTimeZoneFixture"/>, translated to one statement on a connection
/// whose session runs in another zone than UTC, with what it returns over their rows
/// enumerated on the connection of the container, in UTC: the same elements,
/// written to the tick with their kind (<see cref="BuiltValueComparison.Canonical"/>),
/// or the same type of exception.
/// </summary>
internal sealed record SessionTimeZoneComparison
{
    private const string Thrown = "throws ";

    private readonly SessionTimeZoneFixture _fixture;

    private readonly string _zone;

    public SessionTimeZoneComparison(SessionTimeZoneFixture fixture, string zone)
    {
        _fixture = fixture;
        _zone = zone;
    }

    /// <summary>
    /// The query returns the same elements, in any order.
    /// </summary>
    public void AssertSameElements<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        AssertSameOutcome(tables =>
            query(tables)
                .AsEnumerable()
                .Select(BuiltValueComparison.Canonical)
                .Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The query returns the same elements in the same order.
    /// </summary>
    public void AssertSameOrder<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        AssertSameOutcome(tables =>
            query(tables).AsEnumerable().Select(BuiltValueComparison.Canonical)
        );
    }

    /// <summary>
    /// The query returns the same value.
    /// </summary>
    public void AssertSameValue<T>(Func<ExecutionTables, T> query)
    {
        AssertSameOutcome(tables => [BuiltValueComparison.Canonical(query(tables))]);
    }

    private void AssertSameOutcome(Func<ExecutionTables, IEnumerable<string>> query)
    {
        string[] expected = Outcome(() =>
            query(ExecutionTables.Enumerated(_fixture.Execution))
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection(_zone)
        );

        string[] actual = Outcome(() =>
            query(ExecutionTables.Stored(_fixture.Execution, connection))
        );

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
        if (
            expected is not [string only]
            || !only.StartsWith(Thrown, StringComparison.Ordinal)
        )
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
}
