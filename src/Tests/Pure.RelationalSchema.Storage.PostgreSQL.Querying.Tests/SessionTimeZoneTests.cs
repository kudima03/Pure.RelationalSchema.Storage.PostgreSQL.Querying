using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A stored <c>datetime</c> holds no time zone, and its cell text is read in memory
/// as it is written, or assuming UTC: what a query reads of it must not depend on
/// the <c>TimeZone</c> of the session the statement runs in. Each query runs on a
/// connection whose session is ahead of UTC, or behind it, and is compared with the
/// same query over the rows enumerated in UTC, at the edges of the range of
/// <see cref="DateTime"/>, around midnight and at the end of a year. A parse of the
/// cell text, <c>DateTime.Parse(text, invariant)</c>, is read as the column; a parse
/// assuming UTC (<c>AssumeUniversal | AdjustToUniversal</c>) stays a parse of the
/// text, which the translator reads as a <c>timestamptz</c>, whose parts PostgreSQL
/// takes in the session's zone.
/// </summary>
public sealed record SessionTimeZoneTests : IClassFixture<SessionTimeZoneFixture>
{
    private const string Ahead = SessionTimeZoneFixture.Ahead;

    private const string Behind = SessionTimeZoneFixture.Behind;
    private static readonly DateTimeStyles Universal =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly SessionTimeZoneFixture _fixture;

    public SessionTimeZoneTests(SessionTimeZoneFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.Execution.ValueId;

    private IColumn Moment => _fixture.Execution.Moment;

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void EnumeratesStoredDateTimesAsTheyAreStored(string zone)
    {
        string[] stored = Cells(
            ExecutionTables.Stored(_fixture.Execution, _fixture.Connection(zone))
        );

        Assert.Equal(Cells(ExecutionTables.Enumerated(_fixture.Execution)), stored);
        Assert.Contains("2=01/01/0001 00:00:00", stored);
        Assert.Contains("3=12/31/9999 23:59:59.999999", stored);
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void WritesTheCellTextOfStoredDateTimes(string zone)
    {
        SessionTimeZoneComparison comparison = Comparison(zone);

        comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string>(
                x.Cells[ValueId].Value.TextValue,
                x.Cells[Moment].Value.TextValue
            ))
        );
        comparison.AssertSameElements(db =>
            db.Values.Where(x =>
                    x.Cells[Moment].Value.TextValue == "12/31/2024 12:34:56.12"
                    || x.Cells[Moment].Value.TextValue == "01/01/0001 00:00:00"
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone).AssertSameElements(db => Bare(db.Values));
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheDatePartsOfStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Bare(db.Values)
                    .Select(x => new ValueTuple<string, int, int, int, int, int>(
                        x.Item1,
                        x.Item2.Year,
                        x.Item2.Month,
                        x.Item2.Day,
                        x.Item2.DayOfYear,
                        (int)x.Item2.DayOfWeek
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheTimePartsOfStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Bare(db.Values)
                    .Select(x => new ValueTuple<string, int, int, int, int>(
                        x.Item1,
                        x.Item2.Hour,
                        x.Item2.Minute,
                        x.Item2.Second,
                        x.Item2.Millisecond
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheDayAndTheTimeOfDayOfStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Bare(db.Values)
                    .Select(x => new ValueTuple<string, DateTime, TimeSpan>(
                        x.Item1,
                        x.Item2.Date,
                        x.Item2.TimeOfDay
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsGuardedStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                db.Values.Select(x => new ValueTuple<string, DateTime?>(
                        x.Cells[ValueId].Value.TextValue,
                        string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                            ? null
                            : DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                    ))
                    .Select(x => new ValueTuple<string, int, int>(
                        x.Item1,
                        x.Item2.HasValue ? x.Item2.Value.Year : -1,
                        x.Item2.HasValue ? x.Item2.Value.Hour : -1
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ComparesStoredDateTimesParsedBareWithCapturedOnes(string zone)
    {
        DateTime since = new DateTime(2000, 1, 1);
        DateTime until = new DateTime(2024, 12, 31, 12, 34, 56, 120);
        SessionTimeZoneComparison comparison = Comparison(zone);

        comparison.AssertSameElements(db =>
            Bare(db.Values)
                .Where(x => x.Item2 >= since && x.Item2 < until)
                .Select(x => x.Item1)
        );
        comparison.AssertSameElements(db =>
            Bare(db.Values).Where(x => x.Item2 == until).Select(x => x.Item1)
        );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ShiftsStoredDateTimesParsedBare(string zone)
    {
        DateTime since = new DateTime(1999, 12, 31);

        Comparison(zone)
            .AssertSameElements(db =>
                Bare(db.Values)
                    .Where(x => x.Item2.Year > 1 && x.Item2.Year < 9999)
                    .Select(x => new ValueTuple<
                        string,
                        DateTime,
                        DateTime,
                        DateTime,
                        TimeSpan
                    >(
                        x.Item1,
                        x.Item2.AddDays(30),
                        x.Item2.AddHours(-5),
                        x.Item2.AddMonths(1),
                        x.Item2 - since
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void OrdersStoredDateTimesParsedBare(string zone)
    {
        Comparison(zone)
            .AssertSameOrder(db =>
                Bare(db.Values)
                    .OrderByDescending(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Select(x => x.Item1)
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void GroupsStoredDateTimesParsedBareByTheirDay(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Bare(db.Values)
                    .GroupBy(x => x.Item2.Date)
                    .Select(x => new ValueTuple<DateTime, int, DateTime>(
                        x.Key,
                        x.Count(),
                        x.Max(row => row.Item2)
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsStoredDateTimesOfWholeSecondsParsedAsUniversal(string zone)
    {
        Comparison(zone).AssertSameElements(db => UniversalWholeSeconds(db.Values));
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void OrdersStoredDateTimesOfWholeSecondsParsedAsUniversal(string zone)
    {
        Comparison(zone)
            .AssertSameOrder(db =>
                UniversalWholeSeconds(db.Values)
                    .OrderBy(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Select(x => x.Item1)
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheDatePartsOfStoredDateTimesParsedAsUniversal(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Universally(db.Values)
                    .Select(x => new ValueTuple<string, int, int, int, int, int>(
                        x.Item1,
                        x.Item2.Year,
                        x.Item2.Month,
                        x.Item2.Day,
                        x.Item2.DayOfYear,
                        (int)x.Item2.DayOfWeek
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheTimePartsOfStoredDateTimesParsedAsUniversal(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                Universally(db.Values)
                    .Select(x => new ValueTuple<string, int, int, int>(
                        x.Item1,
                        x.Item2.Hour,
                        x.Item2.Minute,
                        x.Item2.Second
                    ))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ReadsTheDayOfStoredDateTimesParsedAsUniversal(string zone)
    {
        Comparison(zone)
            .AssertSameElements(db =>
                UniversalWholeSeconds(db.Values)
                    .Select(x => new ValueTuple<string, DateTime>(x.Item1, x.Item2.Date))
            );
    }

    [Theory]
    [InlineData(Ahead)]
    [InlineData(Behind)]
    public void ComparesStoredDateTimesParsedAsUniversalWithCapturedOnes(string zone)
    {
        DateTime since = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Comparison(zone)
            .AssertSameElements(db =>
                UniversalWholeSeconds(db.Values)
                    .Where(x => x.Item2 >= since)
                    .Select(x => x.Item1)
            );
    }

    [Fact]
    public void ShiftsStoredDateTimesParsedAsUniversalAcrossDaylightSavingTime()
    {
        Comparison(Behind)
            .AssertSameElements(db =>
                UniversalWholeSeconds(db.Values)
                    .Where(x => x.Item2.Year > 1 && x.Item2.Year < 9999)
                    .Select(x => new ValueTuple<string, DateTime, DateTime>(
                        x.Item1,
                        x.Item2.AddDays(30),
                        x.Item2.AddMonths(1)
                    ))
            );
    }

    private SessionTimeZoneComparison Comparison(string zone)
    {
        return new SessionTimeZoneComparison(_fixture, zone);
    }

    private string[] Cells(ExecutionTables tables)
    {
        return
        [
            .. tables
                .Values.AsEnumerable()
                .Select(x =>
                    x.Cells[ValueId].Value.TextValue
                    + "="
                    + x.Cells[Moment].Value.TextValue
                )
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The id of each row with a moment, and the moment parsed as the cell writes
    /// it, which reads the column itself.
    /// </summary>
    private IQueryable<ValueTuple<string, DateTime>> Bare(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[Moment].Value.TextValue != "")
            .Select(x => new ValueTuple<string, DateTime>(
                x.Cells[ValueId].Value.TextValue,
                DateTime.Parse(
                    x.Cells[Moment].Value.TextValue,
                    CultureInfo.InvariantCulture
                )
            ));
    }

    /// <summary>
    /// The id of each row with a moment, and the moment parsed assuming UTC.
    /// </summary>
    private IQueryable<ValueTuple<string, DateTime>> Universally(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[Moment].Value.TextValue != "")
            .Select(x => new ValueTuple<string, DateTime>(
                x.Cells[ValueId].Value.TextValue,
                DateTime.Parse(
                    x.Cells[Moment].Value.TextValue,
                    CultureInfo.InvariantCulture,
                    Universal
                )
            ));
    }

    /// <summary>
    /// The rows of <see cref="Universally"/> whose moment is a whole second, which
    /// the parse keeps whole (it drops a fraction, issue #110).
    /// </summary>
    private IQueryable<ValueTuple<string, DateTime>> UniversalWholeSeconds(
        IQueryable<IRow> rows
    )
    {
        return Universally(
            rows.Where(x => !x.Cells[Moment].Value.TextValue.Contains('.'))
        );
    }
}
