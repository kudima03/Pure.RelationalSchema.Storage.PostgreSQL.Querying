using System.Data;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Translated queries whose results hold dates and times must run as one statement
/// and return what the same query returns over the enumerated rows in memory.
/// </summary>
public sealed record DateTimeResultTests : IClassFixture<QueryingFixture>, IDisposable
{
    private static readonly DateTime Since = new(2024, 1, 1, 10, 20, 30);

    private static readonly DateOnly Day = new(2024, 2, 29);

    private static readonly TimeOnly Time = new(22, 30, 15);

    private static readonly TimeSpan Span = new(1, 2, 3, 4);

    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    public DateTimeResultTests(QueryingFixture fixture)
    {
        _fixture = fixture;
        _connection = new RecordingConnection(fixture.Connection);
    }

    private IColumn Total => _fixture.OrderTotal;

    [Fact]
    public void ReadsDateTimesOfAnonymousTypes()
    {
        DateTime since = Since;

        var orders = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Since = since,
                    Shifted = since.AddDays(x.Cells[Total].Value.TextValue.Length),
                })
        );

        AssertUnspecified(orders.SelectMany(x => new[] { x.Since, x.Shifted }));
    }

    [Fact]
    public void ReadsDateOnlysOfAnonymousTypes()
    {
        DateOnly day = Day;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Day = day,
                    Shifted = day.AddDays(x.Cells[Total].Value.TextValue.Length),
                })
        );
    }

    [Fact]
    public void ReadsTimeOnlysOfAnonymousTypes()
    {
        TimeOnly time = Time;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Time = time,
                    Shifted = time.AddHours(x.Cells[Total].Value.TextValue.Length),
                })
        );
    }

    [Fact]
    public void ReadsTimeSpansOfAnonymousTypes()
    {
        DateTime since = Since;
        TimeSpan span = Span;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Span = span,
                    Elapsed = since.AddDays(x.Cells[Total].Value.TextValue.Length)
                        - since,
                })
        );
    }

    [Fact]
    public void ReadsDateTimesOfValueTuples()
    {
        DateTime since = Since;

        ValueTuple<string, DateTime>[] orders = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new ValueTuple<string, DateTime>(
                    x.Cells[Total].Value.TextValue,
                    since.AddHours(x.Cells[Total].Value.TextValue.Length)
                ))
        );

        AssertUnspecified(orders.Select(x => x.Item2));
    }

    [Fact]
    public void ReadsDateOnlysOfValueTuples()
    {
        DateOnly day = Day;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new ValueTuple<string, DateOnly>(
                    x.Cells[Total].Value.TextValue,
                    day.AddDays(x.Cells[Total].Value.TextValue.Length)
                ))
        );
    }

    [Fact]
    public void ReadsTimeOnlysOfValueTuples()
    {
        TimeOnly time = Time;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new ValueTuple<string, TimeOnly>(
                    x.Cells[Total].Value.TextValue,
                    time.AddHours(x.Cells[Total].Value.TextValue.Length)
                ))
        );
    }

    [Fact]
    public void ReadsTimeSpansOfValueTuples()
    {
        DateTime since = Since;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new ValueTuple<string, TimeSpan>(
                    x.Cells[Total].Value.TextValue,
                    since.AddHours(x.Cells[Total].Value.TextValue.Length) - since
                ))
        );
    }

    [Fact]
    public void ReadsNullableDateTimes()
    {
        DateTime since = Since;

        var orders = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Since = (DateTime?)
                        since.AddDays(x.Cells[Total].Value.TextValue.Length),
                    Missing = (DateTime?)null,
                })
        );

        AssertUnspecified(orders.Select(x => x.Since!.Value));
    }

    [Fact]
    public void ReadsNullableDateOnlys()
    {
        DateOnly day = Day;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Day = (DateOnly?)day.AddDays(x.Cells[Total].Value.TextValue.Length),
                    Missing = (DateOnly?)null,
                })
        );
    }

    [Fact]
    public void ReadsNullableTimeOnlys()
    {
        TimeOnly time = Time;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Time = (TimeOnly?)
                        time.AddHours(x.Cells[Total].Value.TextValue.Length),
                    Missing = (TimeOnly?)null,
                })
        );
    }

    [Fact]
    public void ReadsNullableTimeSpans()
    {
        DateTime since = Since;

        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => new
                {
                    Total = x.Cells[Total].Value.TextValue,
                    Elapsed = (TimeSpan?)(
                        since.AddDays(x.Cells[Total].Value.TextValue.Length) - since
                    ),
                    Missing = (TimeSpan?)null,
                })
        );
    }

    [Fact]
    public void ReadsDateTimeScalars()
    {
        DateTime since = Since;

        DateTime[] constants = AssertLikeInMemory(rows =>
            Ordered(rows).Select(x => since)
        );
        DateTime[] computed = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => since.AddDays(x.Cells[Total].Value.TextValue.Length))
        );

        AssertUnspecified([.. constants, .. computed]);
    }

    [Fact]
    public void ReadsDateOnlyScalars()
    {
        DateOnly day = Day;

        _ = AssertLikeInMemory(rows => Ordered(rows).Select(x => day));
        _ = AssertLikeInMemory(rows =>
            Ordered(rows).Select(x => day.AddDays(x.Cells[Total].Value.TextValue.Length))
        );
    }

    [Fact]
    public void ReadsTimeOnlyScalars()
    {
        TimeOnly time = Time;

        _ = AssertLikeInMemory(rows => Ordered(rows).Select(x => time));
        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => time.AddHours(x.Cells[Total].Value.TextValue.Length))
        );
    }

    [Fact]
    public void ReadsTimeSpanScalars()
    {
        DateTime since = Since;
        TimeSpan span = Span;

        _ = AssertLikeInMemory(rows => Ordered(rows).Select(x => span));
        _ = AssertLikeInMemory(rows =>
            Ordered(rows)
                .Select(x => since.AddDays(x.Cells[Total].Value.TextValue.Length) - since)
        );
    }

    [Fact]
    public void ReadsTheMaximumOfDateTimes()
    {
        DateTime since = Since;

        DateTime maximum = AssertValueLikeInMemory(rows =>
            rows.Max(x => since.AddDays(x.Cells[Total].Value.TextValue.Length))
        );

        AssertUnspecified([maximum]);
    }

    [Fact]
    public void ReadsTheMaximumOfDateOnlys()
    {
        DateOnly day = Day;

        _ = AssertValueLikeInMemory(rows =>
            rows.Max(x => day.AddDays(x.Cells[Total].Value.TextValue.Length))
        );
    }

    [Fact]
    public void ReadsTheMinimumOfTimeOnlys()
    {
        TimeOnly time = Time;

        _ = AssertValueLikeInMemory(rows =>
            rows.Min(x => time.AddHours(x.Cells[Total].Value.TextValue.Length))
        );
    }

    [Fact]
    public void ReadsTheMaximumOfTimeSpans()
    {
        DateTime since = Since;

        _ = AssertValueLikeInMemory(rows =>
            rows.Max(x => since.AddDays(x.Cells[Total].Value.TextValue.Length) - since)
        );
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IQueryable<IRow> Ordered(IQueryable<IRow> rows)
    {
        return rows.OrderBy(x => x.Cells[Total].Value.TextValue);
    }

    private IQueryable<IRow> Orders(IDbConnection connection)
    {
        return new PostgreSqlStoredSchemaDataSet(_fixture.Shop, connection)[
            _fixture.Orders
        ];
    }

    /// <summary>
    /// Runs the query over the table and over its rows enumerated in memory, asserts
    /// that both return the same elements and that the table ran it as one
    /// statement, and returns what the table returned.
    /// </summary>
    private T[] AssertLikeInMemory<T>(Func<IQueryable<IRow>, IQueryable<T>> query)
    {
        return AssertValueLikeInMemory<T[]>(rows => [.. query(rows)]);
    }

    private T AssertValueLikeInMemory<T>(Func<IQueryable<IRow>, T> query)
    {
        T expected = query(
            new EnumerableQuery<IRow>([.. Orders(_fixture.Connection).AsEnumerable()])
        );
        int statements = _connection.Statements.Count;

        T actual = query(Orders(_connection));

        Assert.Equal(expected, actual);
        Assert.Equal(statements + 1, _connection.Statements.Count);
        return actual;
    }

    private static void AssertUnspecified(IEnumerable<DateTime> moments)
    {
        Assert.All(moments, x => Assert.Equal(DateTimeKind.Unspecified, x.Kind));
    }
}
