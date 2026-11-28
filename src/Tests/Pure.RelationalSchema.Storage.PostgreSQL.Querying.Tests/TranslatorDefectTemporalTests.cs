using System.Globalization;
using System.Linq.Expressions;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The shapes of the translator's defects of dates and times built from parts
/// (translator issue #128) over stored tables: the members, comparisons, arithmetic
/// and set operations of <c>DateTime</c>s of the kind <c>Utc</c> built from the
/// parts of cells, run on a connection of its own whose session time zone is not
/// UTC; <c>new DateTime(ticks)</c> out of range; constant parts out of range in a
/// branch no row reaches; <c>TimeOnly</c> minus <c>TimeOnly</c> around midnight; and
/// dates shifted past 9999-12-31 or before 0001-01-01. Each returns in one statement
/// what it returns over the enumerated rows, or fails on the server where .NET throws.
/// </summary>
public sealed record TranslatorDefectTemporalTests : IClassFixture<ExecutionFixture>
{
    private const string Thrown = "throws ";

    private const string SessionTimeZone = "America/New_York";

    /// <summary>
    /// The year of a DateTime of ticks before 0001-01-01 or past 9999-12-31, from
    /// an id of 1 or more.
    /// </summary>
    private static readonly Expression<Func<long, int>>[] TicksOutOfRange =
    [
        x => new DateTime(-TimeSpan.TicksPerDay * x).Year,
        x => new DateTime(DateTime.MaxValue.Ticks + x).Year,
        x => new DateTime(-x, DateTimeKind.Utc).Year,
    ];

    /// <summary>
    /// The year of a date or a DateTime built at a bound of the calendar from the id
    /// 1 and shifted past it.
    /// </summary>
    private static readonly Expression<Func<int, int>>[] ShiftedPastBounds =
    [
        x => new DateOnly(9999, 12, 31 * x).AddDays(1).Year,
        x => new DateOnly(x, 1, 1).AddDays(-1).Year,
        x => DateOnly.FromDayNumber(3_652_058 * x).AddMonths(1).Year,
        x => new DateTime(9999, 12, 31 * x, 23, 0, 0).AddHours(1).Year,
        x => new DateTime(x, 1, 1).AddYears(-1).Year,
    ];

    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public TranslatorDefectTemporalTests(
        ExecutionFixture fixture,
        ITestOutputHelper output
    )
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Flag => _fixture.Flag;

    private IColumn Moment => _fixture.Moment;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Clock => _fixture.Clock;

    private BuiltValueComparison Comparison =>
        new BuiltValueComparison(_fixture, _output);

    [Fact]
    public void ReadsUtcDateTimesAndTheMembersOfUnspecifiedOnesInAnySessionTimeZone()
    {
        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateTime, int, DateTime, TimeSpan>(
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, x, 0, 0).Hour,
                    new DateTime(2024, 3, 9, 12, x, 0).AddDays(1),
                    new DateTime(2024, 3, 10, x, 0, 0)
                        - new DateTime(2024, 3, 9, 12, 0, 0)
                ))
        );
    }

    [Fact]
    public void ReadsTheMembersOfUtcDateTimesBuiltFromPartsAsWritten()
    {
        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, int, int, int>(
                    x,
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc).Hour,
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc).Day,
                    new DateTime(
                        (x * TimeSpan.TicksPerHour) + DateTime.UnixEpoch.Ticks,
                        DateTimeKind.Utc
                    ).Hour
                ))
        );
    }

    [Fact]
    public void ReadsTheDatesAndTimesOfUtcDateTimesBuiltFromPartsAsWritten()
    {
        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, DateTime, DateOnly, TimeOnly>(
                    x,
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc).Date,
                    DateOnly.FromDateTime(
                        new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc)
                    ),
                    TimeOnly.FromDateTime(
                        new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc)
                    )
                ))
        );
    }

    [Fact]
    public void ComparesUtcDateTimesBuiltFromPartsByTheirTicks()
    {
        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, bool, bool>(
                    x,
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc)
                        > new DateTime(2024, 1, 1, 3, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc)
                        > new DateTime(2024, 1, 1, 3, 0, 0)
                ))
        );
    }

    [Fact]
    public void ComparesUtcDateTimesBuiltFromThePartsOfCellsWithTheCells()
    {
        long[] ids = [1, 4, 6, 7, 8, 11, 13, 92];

        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x =>
                    DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
                )
                .Select(x => new ValueTuple<DateTime, bool, bool>(
                    x,
                    new DateTime(
                        x.Year,
                        x.Month,
                        x.Day,
                        x.Hour,
                        x.Minute,
                        x.Second,
                        DateTimeKind.Utc
                    ) <= x,
                    new DateTime(
                        x.Year,
                        x.Month,
                        x.Day,
                        x.Hour,
                        x.Minute,
                        x.Second,
                        DateTimeKind.Utc
                    ) > x.AddHours(-1)
                ))
        );
    }

    [Fact]
    public void ComputesWithUtcDateTimesBuiltFromPartsByTheirTicks()
    {
        AssertSameElementsInSessionTimeZone(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, TimeSpan, DateTime>(
                    x,
                    new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc)
                        - new DateTime(2024, 1, 1, 0, 0, 0),
                    new DateTime(2024, 3, 9, 12, x, 0, DateTimeKind.Utc).AddDays(1)
                ))
        );
    }

    // A column is a timestamp or a timestamptz: no column holds the values of the
    // kind Utc of one side and those of the kind Unspecified of the other.
    [Fact]
    public void RefusesUnitingUtcDateTimesBuiltFromPartsWithStoredDateTimes()
    {
        new ExecutionComparison(_fixture).AssertNotSupported(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(2024, 1, 1, x, 0, 0, DateTimeKind.Utc))
                .Union(
                    db.Values.Where(x =>
                            long.Parse(x.Cells[ValueId].Value.TextValue) == 1
                            || long.Parse(x.Cells[ValueId].Value.TextValue) == 8
                        )
                        .Select(x =>
                            DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                        )
                )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FailsOnTicksOutOfRange(int shape)
    {
        Expression<Func<long, int>> year = TicksOutOfRange[shape];
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(year)
        );
    }

    [Theory(
        Skip = "PostgreSQL computes dates past 9999-12-31 and before 0001-01-01 (the "
            + "year 10000, or 1 BC) where .NET throws."
    )]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void FailsWhenShiftedPastABound(int shape)
    {
        Expression<Func<int, int>> year = ShiftedPastBounds[shape];
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(year)
        );
    }

    [Fact]
    public void LeavesConstantPartsOutOfRangeBesideCellsInABranchNoRowReaches()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Flag].Value.TextValue == "True")
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, TimeOnly, TimeOnly, DateTime, DateTime>(
                    x,
                    x > 100 ? new TimeOnly(x, 0, 60) : TimeOnly.MinValue,
                    x > 100 ? new TimeOnly(x, 0, 0, 1000) : TimeOnly.MinValue,
                    x > 100 ? new DateTime(x, 1, 1, 24, 0, 0) : DateTime.MinValue,
                    x > 100 ? new DateTime(x, 1, 1, 0, 0, 0, 0, 1000) : DateTime.MinValue
                ))
        );
    }

    [Fact]
    public void LeavesConstantValuesOutOfRangeInABranchNoRowReaches()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Flag].Value.TextValue == "True")
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, TimeOnly, DateTime>(
                    x,
                    x > 100 ? new TimeOnly(0, 0, 60) : TimeOnly.MinValue,
                    x > 100 ? new DateTime(2024, 1, 1, 24, 0, 0) : DateTime.MinValue
                ))
        );
    }

    [Fact]
    public void SubtractsStoredTimesAroundMidnight()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue))
                .Select(x => new ValueTuple<TimeOnly, int>(
                    TimeOnly.Parse(
                        x.Cells[Clock].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ),
                    (int)long.Parse(x.Cells[ExtraId].Value.TextValue)
                ))
                .Select(x => new ValueTuple<TimeOnly, TimeSpan, TimeSpan>(
                    x.Item1,
                    x.Item1 - new TimeOnly(12, 0),
                    x.Item1 - new TimeOnly(12, x.Item2)
                ))
        );
    }

    /// <summary>
    /// The query returns over the stored tables, on a connection of its own whose
    /// session time zone is <see cref="SessionTimeZone"/>, the elements, in any order
    /// and to the tick with their kind, that it returns over the enumerated rows, in
    /// one statement.
    /// </summary>
    private void AssertSameElementsInSessionTimeZone<T>(
        Func<ExecutionTables, IQueryable<T>> query
    )
    {
        string[] expected = Outcome(() =>
            query(ExecutionTables.Enumerated(_fixture))
                .AsEnumerable()
                .Select(BuiltValueComparison.Canonical)
        );

        using NpgsqlConnection zoned = ((NpgsqlConnection)_fixture.Connection).CloneWith(
            _fixture.Connection.ConnectionString
        );
        zoned.Open();
        using (NpgsqlCommand command = zoned.CreateCommand())
        {
            command.CommandText = $"SET TimeZone = '{SessionTimeZone}'";
            _ = command.ExecuteNonQuery();
        }

        using RecordingConnection connection = new RecordingConnection(zoned);

        string[] actual = Outcome(() =>
            query(ExecutionTables.Stored(_fixture, connection))
                .AsEnumerable()
                .Select(BuiltValueComparison.Canonical)
        );

        foreach (string statement in connection.Statements)
        {
            _output.WriteLine(statement);
        }
        Assert.Equal(expected, actual);
        _ = Assert.Single(connection.Statements);
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
