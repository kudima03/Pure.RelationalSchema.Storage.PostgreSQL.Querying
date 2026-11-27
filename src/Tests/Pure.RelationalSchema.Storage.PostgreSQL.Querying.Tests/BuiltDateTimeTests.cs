using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Dates and times built from parts read from stored cells — <c>new DateOnly</c>,
/// <c>new TimeOnly</c>, <c>new DateTime</c> with and without a kind, from ticks and
/// day numbers — return in one statement what they return over the enumerated rows,
/// to the tick, and fail on the server wherever .NET throws
/// <see cref="ArgumentOutOfRangeException"/>: a month, a day, an hour, a minute, a
/// second or a fraction out of range, a year outside 1..9999, the 29th of February
/// of a year that is not a leap year.
/// </summary>
public sealed record BuiltDateTimeTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public BuiltDateTimeTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Moment => _fixture.Moment;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private BuiltValueComparison Comparison => new BuiltValueComparison(_fixture, _output);

    [Fact]
    public void BuildsDatesFromIntegerPartsOfCells()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, DateOnly, DateOnly>(
                    x,
                    new DateOnly(x * 700, (x % 12) + 1, x * 2),
                    new DateOnly(2000 + x, 12 - (x % 12), 28 - x)
                ))
        );
    }

    [Fact]
    public void BuildsTheFirstAndLastDatesOfTheCalendar()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) <= 2)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateOnly, DateTime, DateTime>(
                    new DateOnly(1 + ((x - 1) * 9998), 1 + ((x - 1) * 11), 1 + ((x - 1) * 30)),
                    new DateTime(
                        1 + ((x - 1) * 9998),
                        1 + ((x - 1) * 11),
                        1 + ((x - 1) * 30),
                        (x - 1) * 23,
                        (x - 1) * 59,
                        (x - 1) * 59
                    ),
                    new DateTime(
                        1 + ((x - 1) * 9998),
                        1 + ((x - 1) * 11),
                        1 + ((x - 1) * 30),
                        (x - 1) * 23,
                        (x - 1) * 59,
                        (x - 1) * 59,
                        (x - 1) * 999,
                        (x - 1) * 999
                    )
                ))
        );
    }

    [Fact]
    public void BuildsTheTwentyNinthOfFebruaryOfLeapYears()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateOnly, DateTime>(
                    new DateOnly(1996 + (x * 4), 2, 29),
                    new DateTime(2000 + (x * 400), 2, 29, x, 0, 0)
                ))
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FailsOnTheTwentyNinthOfFebruaryOfAYearThatIsNotALeapYear(int id)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == id)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateOnly(1800 + (x * 100), 2, 29))
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FailsOnTheTwentyNinthOfFebruaryOfADateTimeThatIsNotALeapYear(int id)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == id)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(1800 + (x * 100), 2, 29, 12, 0, 0))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(13)]
    public void FailsOnAMonthOutOfRange(int month)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateOnly(2000, month - 1 + x, 1))
        );
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 32)]
    [InlineData(4, 31)]
    [InlineData(2, 30)]
    [InlineData(1, -1)]
    public void FailsOnADayOutOfRange(int month, int day)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(2024, month, day - 1 + x))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(int.MaxValue)]
    public void FailsOnAYearOutOfRange(int year)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateOnly(year - 1 + x, 1, 1))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10000)]
    public void FailsOnAYearOfADateTimeOutOfRange(int year)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(year - 1 + x, 6, 15, 1, 2, 3, 4))
        );
    }

    [Fact]
    public void BuildsTimesFromIntegerPartsOfCells()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<TimeOnly, TimeOnly, TimeOnly, TimeOnly>(
                    new TimeOnly(x + 10, x * 4),
                    new TimeOnly(23 - x, 59 - x, x * 4),
                    new TimeOnly(x, x, 60 - x, 1000 - x),
                    new TimeOnly(23, 59, 60 - x, 1000 - x, 1000 - x)
                ))
        );
    }

    [Fact]
    public void BuildsTheLastMicrosecondOfADay()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<TimeOnly, DateTime, DateTime>(
                    new TimeOnly(22 + x, 58 + x, 58 + x, 998 + x, 998 + x),
                    new DateTime(9998 + x, 12, 31, 22 + x, 58 + x, 58 + x, 998 + x, 998 + x),
                    new DateTime(9998 + x, 12, 31, 22 + x, 58 + x, 58 + x, 998 + x)
                ))
        );
    }

    [Theory]
    [InlineData(24, 0, 0, 0, 0)]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, 60, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, 60, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, 1000, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, 1000)]
    [InlineData(0, 0, 0, 0, -1)]
    [InlineData(23, 59, 59, 999, 1000)]
    public void FailsOnATimePartOutOfRange(
        int hour,
        int minute,
        int second,
        int millisecond,
        int microsecond
    )
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new TimeOnly(
                    hour - 1 + x,
                    minute - 1 + x,
                    second - 1 + x,
                    millisecond - 1 + x,
                    microsecond - 1 + x
                ))
        );
    }

    [Theory]
    [InlineData(24, 0, 0, 0)]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, 60, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, 60, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, 1000)]
    [InlineData(0, 0, 0, -1)]
    public void FailsOnADateTimePartOutOfRange(
        int hour,
        int minute,
        int second,
        int millisecond
    )
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(
                    2024,
                    2,
                    29,
                    hour - 1 + x,
                    minute - 1 + x,
                    second - 1 + x,
                    millisecond - 1 + x
                ))
        );
    }

    [Fact]
    public void FailsOnTheSixtiethSecondOfADateTimeWithoutAFraction()
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(2024, 12, 31, 23, 59, 59 + x))
        );
    }

    [Fact]
    public void FailsOnTheTwentyFourthHourOfADateTimeWithoutAFraction()
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(2024, 12, 31, 23 + x, 0, 0))
        );
    }

    [Fact]
    public void BuildsDateTimesOfTheKindUtcAndUnspecified()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateTime, DateTime, DateTime>(
                    new DateTime(2000 + x, x, x, x, x, x, DateTimeKind.Utc),
                    new DateTime(2000 + x, x, x, x, x, x, x * 7, DateTimeKind.Unspecified),
                    new DateTime(x, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #129): a DateTime constant of the kind Utc is written as a TIMESTAMP"
            + " literal and read back with the kind Unspecified."
    )]
    public void ReadsAConstantDateTimeOfTheKindUtc()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateTime>(
                x.Cells[ValueId].Value.TextValue,
                new DateTime(2005, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            ))
        );
    }

    [Fact(
        Skip = "#111 (translator #129): CASE over a DateTime built with the kind Utc (timestamptz) and"
            + " one without (timestamp) is a timestamptz, read with the kind Utc for"
            + " both."
    )]
    public void ChoosesBetweenDateTimesOfDifferentKinds()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x =>
                    x % 2 == 0
                        ? new DateTime(2000 + x, x, 1, 0, 0, 0, DateTimeKind.Utc)
                        : new DateTime(2000 + x, x, 1, 0, 0, 0)
                )
        );
    }

    [Fact]
    public void RefusesADateTimeOfTheKindLocal()
    {
        new ExecutionComparison(_fixture).AssertNotSupported(db =>
            db.Values.Select(x => new DateTime(
                (int)long.Parse(x.Cells[ValueId].Value.TextValue) + 2000,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Local
            ))
        );
    }

    [Fact]
    public void BuildsDateTimesFromTheDatesAndTimesOfCells()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Day].Value.TextValue))
                .Select(x => new ValueTuple<DateTime, DateTime, DateTime>(
                    new DateTime(
                        DateOnly.Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture),
                        TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture)
                    ),
                    new DateTime(
                        DateOnly.Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture),
                        TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture),
                        DateTimeKind.Utc
                    ),
                    DateOnly
                        .Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture)
                        .ToDateTime(
                            TimeOnly.Parse(
                                x.Cells[Clock].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                            DateTimeKind.Utc
                        )
                ))
        );
    }

    [Fact]
    public void BuildsDateTimesFromNullableDatesAndTimesOfCells()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, DateTime?>(
                x.Cells[ExtraId].Value.TextValue,
                string.IsNullOrEmpty(x.Cells[Day].Value.TextValue)
                    ? null
                    : new DateTime(
                        DateOnly.Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture),
                        new TimeOnly(
                            (int)long.Parse(x.Cells[ExtraId].Value.TextValue),
                            0
                        )
                    )
            ))
        );
    }

    [Fact]
    public void TakesTheDatesAndTimesOfStoredDateTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x =>
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<DateOnly, TimeOnly>(
                    DateOnly.FromDateTime(x),
                    TimeOnly.FromDateTime(x)
                ))
        );
    }

    [Fact]
    public void RebuildsStoredDateTimesFromTheirDatesAndTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x =>
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<DateTime, DateTime>(
                    new DateTime(DateOnly.FromDateTime(x), TimeOnly.FromDateTime(x)),
                    DateOnly.FromDateTime(x).ToDateTime(TimeOnly.FromDateTime(x))
                ))
        );
    }

    [Fact]
    public void RebuildsStoredDateTimesFromTheirParts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x =>
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<DateTime, DateOnly, TimeOnly>(
                    new DateTime(x.Year, x.Month, x.Day, x.Hour, x.Minute, x.Second),
                    new DateOnly(x.Year, x.Month, x.Day),
                    new TimeOnly(x.Hour, x.Minute, x.Second)
                ))
        );
    }

    [Fact]
    public void BuildsTimesFromTicksOfWholeMicroseconds()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<TimeOnly, TimeOnly, TimeOnly>(
                    new TimeOnly((x * TimeSpan.TicksPerHour) + (x * 10)),
                    new TimeOnly(TimeSpan.TicksPerDay - (x * 10)),
                    new TimeOnly((x - 1) * 10)
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #130): new TimeOnly(ticks) of a computed count of ticks drops the ticks"
            + " below a microsecond, which .NET keeps."
    )]
    public void BuildsTimesFromTicksBelowAMicrosecond()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new TimeOnly(x * 15))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(TimeSpan.TicksPerDay)]
    [InlineData(long.MaxValue)]
    public void FailsOnTicksOutsideADay(long ticks)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new TimeOnly(ticks - 1 + x))
        );
    }

    [Fact]
    public void BuildsDatesFromTheFirstAndLastDayNumbers()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) <= 2)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateOnly, DateOnly, int>(
                    DateOnly.FromDayNumber(x - 1),
                    DateOnly.FromDayNumber(3_652_059 - x),
                    DateOnly.FromDayNumber(3_652_059 - x).DayNumber
                ))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3_652_059)]
    [InlineData(int.MaxValue)]
    public void FailsOnADayNumberOutOfRange(int number)
    {
        Comparison.AssertBothFail(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => DateOnly.FromDayNumber(number - 1 + x))
        );
    }

    [Fact]
    public void FiltersAndOrdersByBuiltDates()
    {
        new ExecutionComparison(_fixture).AssertSameOrder(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Where(x =>
                    new DateOnly(
                        2000,
                        1,
                        ((int)long.Parse(x.Cells[ValueId].Value.TextValue) % 5) + 1
                    ) > new DateOnly(2000, 1, 2)
                )
                .OrderByDescending(x => new DateTime(
                    2000,
                    1,
                    ((int)long.Parse(x.Cells[ValueId].Value.TextValue) % 5) + 1,
                    (int)long.Parse(x.Cells[ValueId].Value.TextValue),
                    0,
                    0
                ))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void GroupsByBuiltDates()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => new DateOnly(
                    2000,
                    ((int)long.Parse(x.Cells[ValueId].Value.TextValue) % 3) + 1,
                    1
                ))
                .Select(x => new ValueTuple<DateOnly, int>(x.Key, x.Count()))
        );
    }

    [Fact]
    public void ReadsThePartsOfBuiltDatesAndTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, int, int, int, int>(
                    new DateOnly(2024, x, 1).DayOfYear,
                    new DateTime(2023, 2, 1).AddDays(x * 3).Day,
                    new TimeOnly(x, 30, 15).Minute,
                    new DateTime(2024, 3, x, x, 0, 0).Hour,
                    (int)new DateOnly(2024, 1, x).DayOfWeek
                ))
        );
    }

    [Fact]
    public void ComparesBuiltTimesWithStoredTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue))
                .Select(x => new ValueTuple<string, bool, bool>(
                    x.Cells[ExtraId].Value.TextValue,
                    TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture)
                        < new TimeOnly(
                            (int)long.Parse(x.Cells[ExtraId].Value.TextValue),
                            0
                        ),
                    TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture)
                        == new TimeOnly(23, 59, 59, 999, 999)
                ))
        );
    }

    [Fact]
    public void BuildsDatesFromNullableParts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateOnly?>(
                x.Cells[ValueId].Value.TextValue,
                string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                    ? null
                    : new DateOnly(
                        2000,
                        1,
                        Math.Abs(int.Parse(x.Cells[Number].Value.TextValue) % 28) + 1
                    )
            ))
        );
    }

    // A TimeSpan is not built from its parts in SQL.
    [Fact]
    public void RefusesTimeSpansBuiltFromParts()
    {
        new ExecutionComparison(_fixture).AssertNotSupported(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<TimeSpan, TimeSpan>(
                    new TimeSpan(x, x * 7, x * 90),
                    new TimeSpan(-x, 25, -x * 61, 0)
                ))
        );
    }

    [Fact]
    public void BuildsDateTimesOfTheKindUtcWithFractions()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new DateTime(
                    9986 + x,
                    12,
                    31,
                    23,
                    59,
                    59,
                    1000 - x,
                    1000 - (x * 7),
                    DateTimeKind.Utc
                ))
        );
    }

    [Fact]
    public void ShiftsBuiltDatesByMonthsAndYears()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateTime, DateOnly, DateTime>(
                    new DateTime(2024, 1, 28 + (x % 4), x, 0, 0).AddMonths(1),
                    new DateOnly(2024, 2, 28 + (x % 2)).AddYears(x),
                    new DateTime(2023, 12, 31, 0, 0, 0).AddMonths(x).AddDays(-x)
                ))
        );
    }

    [Fact]
    public void BuildsDatesFromTheDayNumbersOfStoredDates()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Day].Value.TextValue))
                .Select(x =>
                    DateOnly.Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<DateOnly, DateOnly, DateTime>(
                    DateOnly.FromDayNumber(x.DayNumber + 1),
                    DateOnly.FromDateTime(new DateTime(x.Year, x.Month, 1)),
                    x.ToDateTime(new TimeOnly(x.Day % 24, x.Month))
                ))
        );
    }

    [Fact]
    public void RebuildsStoredTimesFromTheirPartsAndFractions()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue))
                .Select(x =>
                    TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<TimeOnly, int, int>(
                    new TimeOnly(x.Hour, x.Minute, x.Second, x.Millisecond, x.Microsecond),
                    x.Millisecond,
                    x.Microsecond
                ))
        );
    }

    [Fact]
    public void ComparesBuiltDateTimesWithStoredDateTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x => new ValueTuple<string, bool, bool, bool>(
                    x.Cells[ValueId].Value.TextValue,
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                        == new DateTime(
                            2000,
                            1,
                            ((int)long.Parse(x.Cells[ValueId].Value.TextValue) % 2) + 1,
                            0,
                            0,
                            0
                        ).AddDays(-1),
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                        >= new DateTime(9999, 12, 31, 23, 59, 59, 999, 999),
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                        < new DateTime(
                            1,
                            1,
                            1,
                            0,
                            0,
                            0,
                            0,
                            (int)long.Parse(x.Cells[ValueId].Value.TextValue) % 3
                        )
                ))
        );
    }

    [Fact]
    public void BuildsDateTimesOfTheKindUtcFromTicks()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<DateTime, DateTime>(
                    new DateTime(x * TimeSpan.TicksPerDay * 1000, DateTimeKind.Utc),
                    new DateTime(x * TimeSpan.TicksPerDay * 1000, DateTimeKind.Unspecified)
                ))
        );
    }

    [Fact]
    public void RefusesAKindThatIsNotAConstant()
    {
        new ExecutionComparison(_fixture).AssertNotSupported(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => new DateTime(
                    2000,
                    (int)long.Parse(x.Cells[ValueId].Value.TextValue),
                    1,
                    0,
                    0,
                    0,
                    x.Cells[Flag].Value.TextValue == "True"
                        ? DateTimeKind.Utc
                        : DateTimeKind.Unspecified
                ))
        );
    }

    [Fact]
    public void RefusesADateAtATimeOfTheKindLocal()
    {
        new ExecutionComparison(_fixture).AssertNotSupported(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Day].Value.TextValue))
                .Select(x =>
                    DateOnly
                        .Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture)
                        .ToDateTime(new TimeOnly(1, 0), DateTimeKind.Local)
                )
        );
    }

    [Fact]
    public void ReadsTheTakenBranchBesideAnInvalidConstantDate()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Flag].Value.TextValue == "True")
                .Select(x =>
                    x.Cells[Text].Value.TextValue != "absent"
                        ? new DateOnly(
                            2000,
                            1,
                            (int)long.Parse(x.Cells[ValueId].Value.TextValue)
                        )
                        : new DateOnly(2000, 13, 1)
                )
        );
    }

    [Fact]
    public void ReadsTheTakenBranchBesideAnInvalidTimeOfACell()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Flag].Value.TextValue == "True")
                .Select(x =>
                    x.Cells[Text].Value.TextValue != "absent"
                        ? new TimeOnly((int)long.Parse(x.Cells[ValueId].Value.TextValue), 0)
                        : new TimeOnly(
                            24 + (int)long.Parse(x.Cells[ValueId].Value.TextValue),
                            0
                        )
                )
        );
    }

    [Fact(
        Skip = "#111 (translator #129): FirstOrDefault of a filtered group of DateTimes built with the"
            + " kind Utc gives the default as COALESCE(timestamptz, TIMESTAMP"
            + " '0001-01-01'), read with the kind Utc, where default(DateTime) is"
            + " Unspecified."
    )]
    public void ReadsTheDefaultOfAGroupOfDateTimesOfTheKindUtc()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, DateTime>(
                    x.Key,
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            new DateTime(
                                2000,
                                1,
                                ((int)long.Parse(row.Cells[ValueId].Value.TextValue) % 28) + 1,
                                0,
                                0,
                                0,
                                DateTimeKind.Utc
                            )
                        )
                        .FirstOrDefault()
                ))
        );
    }
}
