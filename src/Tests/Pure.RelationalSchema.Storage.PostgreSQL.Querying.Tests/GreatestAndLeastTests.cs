using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// <c>Max</c> and <c>Min</c> of an array of a few values read from stored cells,
/// <c>GREATEST</c> and <c>LEAST</c> in SQL, return in one statement what they return
/// over the enumerated rows: for numbers at the edges of their types, decimals with
/// trailing zeros, booleans, dates, times, durations and texts compared ordinally;
/// skipping NULLs and <c>null</c> where every value is NULL; for one value. The
/// types PostgreSQL orders otherwise than .NET are refused.
/// </summary>
public sealed record GreatestAndLeastTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public GreatestAndLeastTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Moment => _fixture.Moment;

    private IColumn Code => _fixture.Code;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private IColumn Measure => _fixture.Measure;

    private BuiltValueComparison Comparison => new BuiltValueComparison(_fixture, _output);

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsTheGreatestAndLeastOfLongsAtTheEdges()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, long, long, long>(
                    x.Cells[ValueId].Value.TextValue,
                    new[]
                    {
                        long.Parse(x.Cells[Big].Value.TextValue),
                        int.Parse(x.Cells[Number].Value.TextValue),
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                    }.Max(),
                    new[]
                    {
                        long.Parse(x.Cells[Big].Value.TextValue),
                        int.Parse(x.Cells[Number].Value.TextValue),
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                    }.Min(),
                    new[] { long.Parse(x.Cells[Big].Value.TextValue) }.Max()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfIntsAndShorts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, int, short, short>(
                    x.Cells[ValueId].Value.TextValue,
                    new[]
                    {
                        int.Parse(x.Cells[Number].Value.TextValue),
                        int.Parse(x.Cells[Small].Value.TextValue),
                        -5,
                    }.Min(),
                    new[]
                    {
                        (short)long.Parse(x.Cells[ValueId].Value.TextValue),
                        (short)7,
                    }.Max(),
                    new[]
                    {
                        (short)-long.Parse(x.Cells[ValueId].Value.TextValue),
                        (short)-7,
                    }.Min()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfNullableLongsSkippingNulls()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long?, long?, long?>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                        ? null
                        : long.Parse(x.Cells[Big].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (long?)int.Parse(x.Cells[Number].Value.TextValue),
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (long?)int.Parse(x.Cells[Number].Value.TextValue) - 1,
                }.Min(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (long?)int.Parse(x.Cells[Number].Value.TextValue),
                }.Max()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfDecimalsWithTheirScale()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (decimal)int.Parse(x.Cells[Number].Value.TextValue))
                .Select(x => new ValueTuple<decimal, decimal, decimal, decimal>(
                    new[] { x, 0.00m }.Max(),
                    new[] { 0.00m, x }.Max(),
                    new[] { x - 0.25m, 0.250m, -0.5m }.Min(),
                    new[] { x * 1.5m, 1.50m }.Max()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfBooleans()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, bool?, bool?, bool>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : (bool?)bool.Parse(x.Cells[Flag].Value.TextValue),
                    false,
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : (bool?)bool.Parse(x.Cells[Flag].Value.TextValue),
                    true,
                }.Min(),
                new[]
                {
                    x.Cells[Text].Value.TextValue == "plain",
                    x.Cells[Small].Value.TextValue == "7",
                }.Max()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfDates()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, DateOnly?, DateOnly?>(
                x.Cells[ExtraId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Day].Value.TextValue)
                        ? null
                        : (DateOnly?)
                            DateOnly.Parse(
                                x.Cells[Day].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new DateOnly(2024, 1, 4),
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Day].Value.TextValue)
                        ? null
                        : (DateOnly?)
                            DateOnly.Parse(
                                x.Cells[Day].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new DateOnly(2024, 1, 4),
                    new DateOnly(
                        2023,
                        12,
                        ((int)long.Parse(x.Cells[ExtraId].Value.TextValue) % 28) + 1
                    ),
                }.Min()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, TimeOnly?, TimeOnly?>(
                x.Cells[ExtraId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue)
                        ? null
                        : (TimeOnly?)
                            TimeOnly.Parse(
                                x.Cells[Clock].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new TimeOnly(12, 0),
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue)
                        ? null
                        : (TimeOnly?)
                            TimeOnly.Parse(
                                x.Cells[Clock].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new TimeOnly(12, 0, 0, 100),
                }.Min()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfDateTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateTime?, DateTime?>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                        ? null
                        : (DateTime?)
                            DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new DateTime(2000, 1, 1),
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                        ? null
                        : (DateTime?)
                            DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new DateTime(
                        2000,
                        1,
                        1,
                        (int)long.Parse(x.Cells[ValueId].Value.TextValue) % 24,
                        0,
                        0
                    ),
                }.Min()
            ))
        );
    }

    // A column is a timestamp or a timestamptz: no column holds the greatest of a
    // value of the kind Utc and one of the kind Unspecified, which is of either.
    [Fact]
    public void RefusesTheGreatestOfDateTimesOfDifferentKinds()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, DateTime, DateTime>(
                    x.Cells[ValueId].Value.TextValue,
                    new[]
                    {
                        new DateTime(
                            2000 + (int)long.Parse(x.Cells[ValueId].Value.TextValue),
                            1,
                            1
                        ),
                        new DateTime(2005, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    }.Max(),
                    new[]
                    {
                        new DateTime(
                            2000 + (int)long.Parse(x.Cells[ValueId].Value.TextValue),
                            1,
                            1,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc
                        ),
                        new DateTime(2005, 1, 1),
                    }.Min()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfDurations()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, TimeSpan, TimeSpan>(
                    x.Cells[ValueId].Value.TextValue,
                    new[]
                    {
                        TimeSpan.FromTicks(
                            long.Parse(x.Cells[ValueId].Value.TextValue) * TimeSpan.TicksPerHour
                        ),
                        TimeSpan.FromHours(5),
                    }.Max(),
                    new[]
                    {
                        TimeSpan.FromTicks(
                            long.Parse(x.Cells[ValueId].Value.TextValue) * TimeSpan.TicksPerHour
                        ),
                        TimeSpan.FromDays(0.5),
                        TimeSpan.FromMinutes(-1),
                    }.Min()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfOrdinalTextsSkippingNulls()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string?, string?, string?>(
                x.Cells[ValueId].Value.TextValue,
                new[] { x.Cells[Text].Value.TextValue, "a", "Z", "" }.Min(
                    StringComparer.Ordinal
                ),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue,
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : x.Cells[Flag].Value.TextValue,
                }.Max(StringComparer.Ordinal),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : x.Cells[Flag].Value.TextValue,
                    string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                        ? null
                        : x.Cells[Code].Value.TextValue,
                }.Min(StringComparer.Ordinal)
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestOfOrdinalTextsAboveTheBasicPlane()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 3)
                .Select(x => new ValueTuple<string?, string?>(
                    new[] { x.Cells[Text].Value.TextValue, "Ünicøde Ａ" }.Max(
                        StringComparer.Ordinal
                    ),
                    new[] { x.Cells[Text].Value.TextValue, "Ünicøde Ａ" }.Min(
                        StringComparer.Ordinal
                    )
                ))
        );
    }

    [Fact]
    public void FiltersAndOrdersByTheGreatestOfValues()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Where(x =>
                    new[]
                    {
                        int.Parse(x.Cells[Number].Value.TextValue),
                        int.Parse(x.Cells[Small].Value.TextValue),
                    }.Max() > 0
                )
                .OrderByDescending(x =>
                    new[]
                    {
                        long.Parse(x.Cells[Big].Value.TextValue),
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                    }.Min()
                )
                .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    // GREATEST skips NULL, where the parse of the empty text of the cell fails in
    // memory: the greatest is NULL there, which no number is read from, as for
    // any other value computed from a NULL cell parsed bare.
    [Fact]
    public void FailsOnTheGreatestOfANullCellParsedBare()
    {
        Enumeration.AssertFails<FormatException, InvalidOperationException>(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 91)
                .Select(x => new[] { long.Parse(x.Cells[Big].Value.TextValue), 5L }.Max())
        );
    }

    [Fact]
    public void RefusesTheGreatestOfDoubles()
    {
        Enumeration.AssertNotSupported(db =>
            db.Extras.Where(x => !string.IsNullOrEmpty(x.Cells[Measure].Value.TextValue))
                .Select(x =>
                    new[]
                    {
                        double.Parse(
                            x.Cells[Measure].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ),
                        1.0,
                    }.Max()
                )
        );
    }

    [Fact]
    public void RefusesTheLeastOfGuids()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Code].Value.TextValue))
                .Select(x =>
                    new[] { Guid.Parse(x.Cells[Code].Value.TextValue), Guid.Empty }.Min()
                )
        );
    }

    [Fact]
    public void RefusesTheGreatestOfTextsWithoutTheOrdinalComparer()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x => new[] { x.Cells[Text].Value.TextValue, "m" }.Max())
        );
    }

    [Fact]
    public void RefusesTheGreatestOfTextsWithACultureComparer()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x =>
                new[] { x.Cells[Text].Value.TextValue, "m" }.Max(
                    StringComparer.InvariantCultureIgnoreCase
                )
            )
        );
    }

    [Fact]
    public void ReadsTheGreatestOfANullConstantAndValues()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, int?, int?>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Number].Value.TextValue),
                    null,
                }.Max(),
                new int?[] { null, (int)long.Parse(x.Cells[ValueId].Value.TextValue) }.Min()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfBuiltDatesAndTimes()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 13)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<TimeOnly, DateOnly, DateTime>(
                    new[] { new TimeOnly(x, 0), new TimeOnly(5, 30), new TimeOnly(0, x) }.Max(),
                    new[] { new DateOnly(2000, x, 1), new DateOnly(2000, 6, x) }.Min(),
                    new[]
                    {
                        new DateTime(2000, 1, 1, x, 0, 0),
                        new DateTime(2000, 1, 1, 6, 0, 0, 0, x),
                    }.Max()
                ))
        );
    }

    [Fact]
    public void AggregatesTheGreatestOfValuesOverGroups()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, long, long, int>(
                    x.Key,
                    x.Max(row =>
                        new[]
                        {
                            long.Parse(row.Cells[Big].Value.TextValue),
                            long.Parse(row.Cells[ValueId].Value.TextValue),
                        }.Min()
                    ),
                    x.Sum(row =>
                        new[]
                        {
                            long.Parse(row.Cells[ValueId].Value.TextValue),
                            int.Parse(row.Cells[Small].Value.TextValue),
                        }.Max()
                    ),
                    x.Count(row =>
                        new[]
                        {
                            int.Parse(row.Cells[Number].Value.TextValue),
                            int.Parse(row.Cells[Small].Value.TextValue),
                        }.Min() < 0
                    )
                ))
        );
    }

    [Fact]
    public void RefusesTheGreatestOfUnsignedNumbers()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x =>
                    new[]
                    {
                        (uint)long.Parse(x.Cells[ValueId].Value.TextValue),
                        7u,
                    }.Max()
                )
        );
    }
}
