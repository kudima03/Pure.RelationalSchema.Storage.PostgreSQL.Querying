using System.Globalization;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A value of the <c>Values</c> table, read as its types, which windows partition,
/// order and aggregate.
/// </summary>
internal sealed record WindowItem(
    long Id,
    bool? Flag,
    int? Number,
    long? Big,
    string Text,
    DateTime? Moment
);

/// <summary>
/// Window functions over stored tables, written with the translator's marker
/// <c>PostgreSqlWindows.Over</c>, which throws when called: the provider keeps the
/// call, rewrites the cells read in its lambdas into column properties and reads its
/// results, so that the query runs as one statement and returns the window of each
/// row computed in memory (<see cref="InMemoryWindow{T}"/>) over the enumerated
/// rows: every window function, frames of rows and ranges, partitions holding
/// NULL keys and orders with ties.
/// </summary>
public sealed record WindowTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public WindowTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Moment => _fixture.Moment;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void NumbersTheRowsOfPartitionsHoldingANullKey()
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
            x => (x.Number, x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void NumbersTheRowsInADescendingOrderWithNullsLast()
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderByDescending(x => x.Number).ThenByDescending(x => x.Id),
            x => (x.Number, x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void NumbersTheRowsOfOnePartition()
    {
        AssertWindow(
            null,
            rows => rows.OrderBy(x => x.Moment).ThenBy(x => x.Id),
            x => (x.Moment, x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void RanksTiesInPartitionsHoldingANullKey()
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number),
            x => x.Number,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double, double>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.PercentRank(),
                    w.CumeDist()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double, double>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.PercentRank,
                    w.CumeDist
                )
        );
    }

    [Fact]
    public void RanksTiesOfTwoKeysInOnePartition()
    {
        AssertWindow(
            null,
            rows => rows.OrderByDescending(x => x.Text, StringComparer.Ordinal)
                .ThenBy(x => x.Flag),
            x => (x.Text, x.Flag),
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double, double>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.PercentRank(),
                    w.CumeDist()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double, double>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.PercentRank,
                    w.CumeDist
                )
        );
    }

    [Fact]
    public void RanksEveryRowFirstWithoutAnOrder()
    {
        AssertWindow(
            x => x.Text,
            null,
            x => null,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double, double, long>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.PercentRank(),
                    w.CumeDist(),
                    w.Rows.LongCount()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double, double, long>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.PercentRank,
                    w.CumeDist,
                    w.Peers.LongCount()
                )
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(20)]
    public void DividesPartitionsIntoBuckets(int buckets)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (x, w) => new ValueTuple<long, int>(x.Id, w.Ntile(buckets)),
            (x, w) => new ValueTuple<long, int>(x.Id, w.Ntile(buckets))
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(-3)]
    [InlineData(12)]
    [InlineData(0)]
    public void ReadsTheRowsBeforeAndAfterWithADefault(int offset)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
            x => (x.Number, x.Id),
            null,
            (current, w) =>
                new ValueTuple<long, long, long, string>(
                    current.Id,
                    w.Lag(x => x.Id, offset, -1L),
                    w.Lead(x => x.Id * 10, offset, current.Id),
                    w.Lag(x => x.Text, offset, current.Text + "!")
                ),
            (current, w) =>
                new ValueTuple<long, long, long, string>(
                    current.Id,
                    w.Lag(x => x.Id, offset, -1L),
                    w.Lead(x => x.Id * 10, offset, current.Id),
                    w.Lag(x => x.Text, offset, current.Text + "!")
                )
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-2)]
    public void ReadsNullForAMissingRowWithANullDefault(int offset)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
            x => (x.Number, x.Id),
            null,
            (current, w) =>
                new ValueTuple<long, int?, DateTime?, long?>(
                    current.Id,
                    w.Lag(x => x.Number, offset, null),
                    w.Lead(x => x.Moment, offset, null),
                    w.Lead(x => (long?)x.Id, offset, null)
                ),
            (current, w) =>
                new ValueTuple<long, int?, DateTime?, long?>(
                    current.Id,
                    w.Lag(x => x.Number, offset, null),
                    w.Lead(x => x.Moment, offset, null),
                    w.Lead(x => (long?)x.Id, offset, null)
                )
        );
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    [InlineData(-2, 0)]
    [InlineData(0, 0)]
    public void ReadsTheFirstAndLastValuesOfFramesHoldingTheRow(int? start, int? end)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
            x => (x.Number, x.Id),
            PostgreSqlFrame.Rows(start, end),
            (x, w) =>
                new ValueTuple<long, long, int?, string>(
                    x.Id,
                    w.FirstValue(row => row.Id),
                    w.LastValue(row => row.Number),
                    w.LastValue(row => row.Text)
                ),
            (x, w) =>
                new ValueTuple<long, long, int?, string>(
                    x.Id,
                    w.Rows(start, end).First().Id,
                    w.Rows(start, end).Last().Number,
                    w.Rows(start, end).Last().Text
                )
        );
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(-3, -1)]
    [InlineData(2, null)]
    [InlineData(null, -1)]
    public void ReadsNullAsTheFirstAndLastValuesOfAnEmptyFrame(int? start, int? end)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(start, end),
            (x, w) =>
                new ValueTuple<long, long?, long?>(
                    x.Id,
                    w.FirstValue(row => (long?)row.Id),
                    w.LastValue(row => (long?)row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, long?, long?>(
                    x.Id,
                    w.Rows(start, end).Select(row => (long?)row.Id).FirstOrDefault(),
                    w.Rows(start, end).Select(row => (long?)row.Id).LastOrDefault()
                )
        );
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-2, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    [InlineData(1, 3)]
    [InlineData(-3, -1)]
    public void AggregatesTheRowsOfAFrame(int? start, int? end)
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
            x => (x.Number, x.Id),
            PostgreSqlFrame.Rows(start, end),
            (x, w) =>
                new ValueTuple<long, int, long, long?, int?, long?, double?, (bool, bool)>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.LongCount(row => row.Text == "plain"),
                    w.Rows.Sum(row => (long?)row.Number),
                    w.Rows.Min(row => row.Number),
                    w.Rows.Max(row => row.Big),
                    w.Rows.Average(row => row.Number),
                    new ValueTuple<bool, bool>(
                        w.Rows.Any(row => row.Number < 0),
                        w.Rows.All(row => row.Moment != null)
                    )
                ),
            (x, w) =>
                new ValueTuple<long, int, long, long?, int?, long?, double?, (bool, bool)>(
                    x.Id,
                    w.Rows(start, end).Count(),
                    w.Rows(start, end).LongCount(row => row.Text == "plain"),
                    w.Rows(start, end).Sum(row => (long?)row.Number),
                    w.Rows(start, end).Min(row => row.Number),
                    w.Rows(start, end).Max(row => row.Big),
                    w.Rows(start, end).Average(row => row.Number),
                    new ValueTuple<bool, bool>(
                        w.Rows(start, end).Any(row => row.Number < 0),
                        w.Rows(start, end).All(row => row.Moment != null)
                    )
                )
        );
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, 3)]
    public void AggregatesTheFilteredAndProjectedRowsOfAFrame(int? start, int? end)
    {
        AssertWindow(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(start, end),
            (x, w) =>
                new ValueTuple<long, long, int, long?, int>(
                    x.Id,
                    w.Rows.Where(row => row.Flag == true).Sum(row => row.Id),
                    w.Rows.Where(row => row.Flag != true).Count(row => row.Big < 0),
#pragma warning disable IDE0120 // The aggregates of a projection of the rows are the tree tested.
                    w.Rows.Select(row => (long?)row.Id).Max(),
                    w.Rows.Select(row => row.Text.Length).Sum()
#pragma warning restore IDE0120
                ),
            (x, w) =>
                new ValueTuple<long, long, int, long?, int>(
                    x.Id,
                    w.Rows(start, end).Where(row => row.Flag == true).Sum(row => row.Id),
                    w.Rows(start, end).Where(row => row.Flag != true).Count(row => row.Big < 0),
                    w.Rows(start, end).Max(row => (long?)row.Id),
                    w.Rows(start, end)
                        .Sum(row => new StringInfo(row.Text).LengthInTextElements)
                )
        );
    }

    [Fact]
    public void AggregatesThePeersUpToTheRowInTheDefaultFrame()
    {
        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number),
            x => x.Number,
            null,
            (x, w) =>
                new ValueTuple<long, long, int, long?>(
                    x.Id,
                    w.Rows.Sum(row => row.Id),
                    w.Rows.Count(),
                    w.LastValue(row => (long?)row.Number)
                ),
            (x, w) =>
                new ValueTuple<long, long, int, long?>(
                    x.Id,
                    w.Peers.Sum(row => row.Id),
                    w.Peers.Count(),
                    w.Peers.Last().Number
                )
        );
    }

    [Fact]
    public void AggregatesThePartitionWithoutAnOrder()
    {
        AssertWindow(
            x => x.Flag,
            null,
            x => null,
            null,
            (x, w) =>
                new ValueTuple<long, long, int, int?, bool>(
                    x.Id,
                    w.Rows.Sum(row => row.Id),
                    w.Rows.Count(),
                    w.Rows.Max(row => row.Number),
                    w.Rows.Any(row => row.Moment == null)
                ),
            (x, w) =>
                new ValueTuple<long, long, int, int?, bool>(
                    x.Id,
                    w.Peers.Sum(row => row.Id),
                    w.Peers.Count(),
                    w.Peers.Max(row => row.Number),
                    w.Peers.Any(row => row.Moment == null)
                )
        );
    }

    [Theory]
    [InlineData("-2", "0")]
    [InlineData("-1", "1")]
    [InlineData(null, "0")]
    [InlineData("0", "3")]
    [InlineData("1", null)]
    [InlineData("2", "5")]
    public void AggregatesARangeOfNumbers(string? start, string? end)
    {
        decimal? from = start is null ? null : decimal.Parse(start, CultureInfo.InvariantCulture);
        decimal? to = end is null ? null : decimal.Parse(end, CultureInfo.InvariantCulture);

        AssertWindow(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Range(from, to),
            (x, w) =>
                new ValueTuple<long, int, long, long?>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id),
                    w.FirstValue(row => (long?)row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long, long?>(
                    x.Id,
                    InRange(w, from, to, false).Count(),
                    InRange(w, from, to, false).Sum(row => row.Id),
                    InRange(w, from, to, false).Select(row => (long?)row.Id).FirstOrDefault()
                )
        );
    }

    [Theory]
    [InlineData("-2", "0")]
    [InlineData("0", "3")]
    public void AggregatesARangeOfNumbersInADescendingOrder(string start, string end)
    {
        decimal from = decimal.Parse(start, CultureInfo.InvariantCulture);
        decimal to = decimal.Parse(end, CultureInfo.InvariantCulture);

        AssertWindow(
            null,
            rows => rows.OrderByDescending(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Range(from, to),
            (x, w) => new ValueTuple<long, int, long>(x.Id, w.Rows.Count(), w.Rows.Sum(row => row.Id)),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    InRange(w, from, to, true).Count(),
                    InRange(w, from, to, true).Sum(row => row.Id)
                )
        );
    }

    [Theory]
    [InlineData("-1.5", "0.5", false)]
    [InlineData("-0.0000000000000000000000000001", "0", false)]
    [InlineData("0.25", "2.75", false)]
    [InlineData("-2.5", "0", true)]
    [InlineData(null, "-1.5", true)]
    public void AggregatesARangeOfFractionsOverADecimalKey(
        string? start,
        string end,
        bool descending
    )
    {
        decimal? from = start is null ? null : decimal.Parse(start, CultureInfo.InvariantCulture);
        decimal to = decimal.Parse(end, CultureInfo.InvariantCulture);

        AssertWindow(
            x => x.Flag,
            descending
                ? rows => rows.OrderByDescending(x => (decimal)x.Id / 2)
                : rows => rows.OrderBy(x => (decimal)x.Id / 2),
            x => x.Id,
            PostgreSqlFrame.Range(from, to),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
            {
                WindowItem[] frame =
                [
                    .. w.Range(
                        row => (decimal)row.Id / 2,
                        (row, current) =>
                            descending
                                ? (from is null || row <= current - from) && row >= current - to
                                : (from is null || row >= current + from) && row <= current + to
                    ),
                ];
                return new ValueTuple<long, int, long>(
                    x.Id,
                    frame.Length,
                    frame.Sum(row => row.Id)
                );
            }
        );
    }

    [Theory]
    [InlineData(-366.0, 0.0)]
    [InlineData(-1.0, 1.0)]
    [InlineData(0.0, 36500.0)]
    [InlineData(-0.00000001157407407, 0.0)]
    public void AggregatesARangeOfInstants(double startDays, double endDays)
    {
        TimeSpan start = TimeSpan.FromTicks((long)Math.Round(startDays * TimeSpan.TicksPerDay / 10) * 10);
        TimeSpan end = TimeSpan.FromTicks((long)Math.Round(endDays * TimeSpan.TicksPerDay / 10) * 10);

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Items(db).Where(x => x.Moment != null),
                    null,
                    rows => rows.OrderBy(x => x.Moment),
                    PostgreSqlFrame.Range(start, end),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(
                        Items(db).Where(x => x.Moment != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Moment),
                        x => x.Moment
                    )
                    .Select(x =>
                    {
                        WindowItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Moment!.Value,
                                (row, current) =>
                                    row - current >= start && row - current <= end
                            ),
                        ];
                        return new ValueTuple<long, int, long>(
                            x.Row.Id,
                            frame.Length,
                            frame.Sum(row => row.Id)
                        );
                    })
        );
    }

    [Theory]
    [InlineData(-12, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, 1200)]
    public void AggregatesARangeOfCalendarMonths(int start, int end)
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Items(db).Where(x => x.Moment != null),
                    null,
                    rows => rows.OrderBy(x => x.Moment),
                    PostgreSqlFrame.RangeMonths(start, end),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(
                        Items(db).Where(x => x.Moment != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Moment),
                        x => x.Moment
                    )
                    .Select(x =>
                    {
                        WindowItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Moment!.Value,
                                (row, current) =>
                                    AtLeastMonths(row, current, start)
                                    && !AtLeastMonths(row, current, end, true)
                            ),
                        ];
                        return new ValueTuple<long, int, long>(
                            x.Row.Id,
                            frame.Length,
                            frame.Sum(row => row.Id)
                        );
                    })
        );
    }

    [Fact]
    public void PartitionsByATupleAndAnObjectOfKeys()
    {
        AssertWindow(
            x => new ValueTuple<bool?, string>(x.Flag, x.Text),
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.RowNumber(), w.Rows.LongCount()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.RowNumber, w.Peers.LongCount())
        );
        AssertWindow(
            x => new { x.Flag, Negative = x.Big < 0 },
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void WindowsTheCellsOfStoredRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x =>
                        string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            ? null
                            : (bool?)bool.Parse(x.Cells[Flag].Value.TextValue),
                    rows => rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    PostgreSqlFrame.Rows(-1, 0),
                    (current, w) =>
                        new ValueTuple<string, long, string, long?, int>(
                            current.Cells[ValueId].Value.TextValue,
                            w.RowNumber(),
                            w.Lag(x => x.Cells[Text].Value.TextValue, 1, "none"),
                            w.Rows.Sum(x =>
                                string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                                    ? null
                                    : long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                            current.Cells.Count
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x =>
                            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                                ? null
                                : bool.Parse(x.Cells[Flag].Value.TextValue),
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, long, string, long?, int>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.RowNumber,
                        x.Window.Lag(row => row.Cells[Text].Value.TextValue, 1, "none"),
                        x.Window.Rows(-1, 0)
                            .Sum(row =>
                                string.IsNullOrEmpty(row.Cells[Big].Value.TextValue)
                                    ? null
                                    : long.Parse(row.Cells[ValueId].Value.TextValue)
                            ),
                        x.Row.Cells.Count
                    ))
        );
    }

    [Fact]
    public void ReadsTheStoredRowsOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                        db.Values,
                        x => x.Cells[Text].Value.TextValue,
                        rows => rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                        null,
                        (row, w) => new ValueTuple<IRow, long>(row, w.RowNumber())
                    )
                    .Where(x => x.Item2 == 1)
                    .Select(x => x.Item1),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => x.Cells[Text].Value.TextValue,
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Where(x => x.Window.RowNumber == 1)
                    .Select(x => x.Row)
        );
    }

    [Fact]
    public void FiltersOrdersAndPagesTheResultsOfAWindow()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls.Over(
                        Items(db),
                        x => x.Flag,
                        rows => rows.OrderByDescending(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .Where(x => x.Item2 <= 3)
                    .OrderBy(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Skip(1)
                    .Take(5),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(Items(db), x => x.Flag, rows => rows.OrderByDescending(x => x.Id), x => x.Id)
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .Where(x => x.Item2 <= 3)
                    .OrderBy(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Skip(1)
                    .Take(5)
        );
    }

    [Fact]
    public void ReadsAnElementAndAnAggregateOfTheResultsOfAWindow()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls.Over(
                        Items(db),
                        null,
                        rows => rows.OrderBy(x => x.Big).ThenBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .OrderByDescending(x => x.Item2)
                    .First(),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(Items(db), x => null, rows => rows.OrderBy(x => x.Big).ThenBy(x => x.Id), x => (x.Big, x.Id))
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .OrderByDescending(x => x.Item2)
                    .First()
        );
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls.Over(
                        Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => w.RowNumber() * x.Id
                    )
                    .Sum(),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(Items(db), x => x.Flag, rows => rows.OrderBy(x => x.Id), x => x.Id)
                    .Sum(x => x.Window.RowNumber * x.Row.Id)
        );
    }

    [Fact]
    public void WindowsTheRowsOfAJoin()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.Join(
                        db.Children,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, child) =>
                            new ValueTuple<long, long, string>(
                                long.Parse(parent.Cells[ParentId].Value.TextValue),
                                long.Parse(child.Cells[ChildId].Value.TextValue),
                                parent.Cells[ParentKey].Value.TextValue
                            )
                    ),
                    x => x.Item1,
                    rows => rows.OrderByDescending(x => x.Item2),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, long, long>(
                            x.Item1,
                            x.Item2,
                            w.RowNumber(),
                            w.Rows.Sum(row => row.Item2)
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<long, long, string>>
                    .Over(
                        db.Parents.Join(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, child) =>
                                new ValueTuple<long, long, string>(
                                    long.Parse(parent.Cells[ParentId].Value.TextValue),
                                    long.Parse(child.Cells[ChildId].Value.TextValue),
                                    parent.Cells[ParentKey].Value.TextValue
                                )
                        ),
                        x => x.Item1,
                        rows => rows.OrderByDescending(x => x.Item2),
                        x => x.Item2
                    )
                    .Select(x => new ValueTuple<long, long, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber,
                        x.Window.Peers.Sum(row => row.Item2)
                    ))
        );
    }

    [Fact]
    public void WindowsTheGroupsOfAGrouping()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Items(db)
                        .GroupBy(x => x.Text)
                        .Select(x => new ValueTuple<string, int, long>(
                            x.Key,
                            x.Count(),
                            x.Sum(row => row.Id)
                        )),
                    null,
                    rows => rows.OrderByDescending(x => x.Item2),
                    null,
                    (x, w) => new ValueTuple<long, long, long>(x.Item3, w.Rank(), w.DenseRank())
                ),
            db =>
                InMemoryWindow<ValueTuple<string, int, long>>
                    .Over(
                        Items(db)
                            .GroupBy(x => x.Text)
                            .Select(x => new ValueTuple<string, int, long>(
                                x.Key,
                                x.Count(),
                                x.Sum(row => row.Id)
                            )),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Item2),
                        x => x.Item2
                    )
                    .Select(x => new ValueTuple<long, long, long>(
                        x.Row.Item3,
                        x.Window.Rank,
                        x.Window.DenseRank
                    ))
        );
    }

    [Fact]
    public void WindowsAnInMemorySequenceBesideStoredRows()
    {
        (long Id, string Label)[] labels = [(1, "a"), (2, "b"), (3, "a"), (91, "a"), (5, "b")];

        Enumeration.AssertSameElements(
            db =>
                db.Values.Provider.CreateQuery<ValueTuple<long, long, bool>>(
                    MarkerCalls.OverCall<(long Id, string Label), ValueTuple<long, long, bool>>(
                        labels.AsQueryable().Expression,
                        x => x.Label,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) =>
                            new ValueTuple<long, long, bool>(
                                x.Id,
                                w.RowNumber(),
                                db.Values.Any(row =>
                                    long.Parse(row.Cells[ValueId].Value.TextValue) == x.Id
                                )
                            )
                    )
                ),
            db =>
                InMemoryWindow<(long Id, string Label)>
                    .Over(labels, x => x.Label, rows => rows.OrderBy(x => x.Id), x => x.Id)
                    .Select(x => new ValueTuple<long, long, bool>(
                        x.Row.Id,
                        x.Window.RowNumber,
                        db.Values.Any(row =>
                            long.Parse(row.Cells[ValueId].Value.TextValue) == x.Row.Id
                        )
                    ))
        );
    }

    [Fact]
    public void FiltersRowsByACapturedWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = MarkerCalls.Over(
                    Items(db),
                    x => x.Flag,
                    rows => rows.OrderByDescending(x => x.Id),
                    null,
                    (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                );
                return db.Values.Where(row =>
                        ranked.Any(x =>
                            x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                            && x.Item2 == 1
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                long[] first =
                [
                    .. InMemoryWindow<WindowItem>
                        .Over(Items(db), x => x.Flag, rows => rows.OrderByDescending(x => x.Id), x => x.Id)
                        .Where(x => x.Window.RowNumber == 1)
                        .Select(x => x.Row.Id),
                ];
                return db.Values.Where(row =>
                        first.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void KeepsAWindowCallWhenInliningCapturedQueries()
    {
        IQueryable<IRow> values = ExecutionTables.Stored(_fixture, _fixture.Connection).Values;
        IQueryable<long> ranked = MarkerCalls.Over(
            values.Where(x => x.Cells[Text].Value.TextValue == "plain"),
            null,
            rows => rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
            null,
            (x, w) => w.RowNumber()
        );
        IQueryable<int> query = values.Select(x => ranked.Count());

        Expression inlined = new CapturedQueries().Visit(query.Expression)!;

        MethodCallExpression over = Assert.Single(new MarkerCallFinder().In(inlined));
        Assert.Equal(nameof(PostgreSqlWindows.Over), over.Method.Name);
        MethodCallExpression source = Assert.IsType<MethodCallExpression>(over.Arguments[0], exactMatch: false);
        _ = Assert.IsType<PostgreSqlStoredTableDataSet>(
            Assert.IsType<ConstantExpression>(source.Arguments[0], exactMatch: false).Value
        );
    }

    [Fact]
    public void RefusesADistinctAggregateOfAFrame()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Items(db),
                null,
                null,
                null,
                (x, w) => w.Rows.Select(row => row.Text).Distinct().Count()
            )
        );
    }

    [Fact]
    public void RefusesAWindowReadInASubqueryOfTheSelector()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Items(db),
                null,
                rows => rows.OrderBy(x => x.Id),
                null,
                (x, w) => db.Children.LongCount(row => w.RowNumber() > 1)
            )
        );
    }

    [Fact]
    public void RefusesARangeOfOffsetsOverTwoSortKeys()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Items(db),
                null,
                rows => rows.OrderBy(x => x.Id).ThenBy(x => x.Number),
                PostgreSqlFrame.Range(-1m, 0m),
                (x, w) => w.Rows.Count()
            )
        );
    }

    [Fact]
    public void RefusesAWindowOverTablesOfTwoConnections()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> children = ExecutionTables.Stored(_fixture, other).Children;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls.Over(
                    ExecutionTables.Stored(_fixture, one).Values,
                    null,
                    null,
                    null,
                    (x, w) => children.Count()
                )
                .ToList()
        );

        Assert.Equal(
            "The query operator Over reads tables of more than one connection, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    /// <summary>
    /// The rows of the <c>RANGE</c> frame of offsets of the id.
    /// </summary>
    private static IEnumerable<WindowItem> InRange(
        InMemoryWindow<WindowItem> window,
        decimal? start,
        decimal? end,
        bool descending
    )
    {
        return window.Range(
            row => (decimal)row.Id,
            (row, current) =>
                descending
                    ? (start is null || row <= current - start)
                        && (end is null || row >= current - end)
                    : (start is null || row >= current + start)
                        && (end is null || row <= current + end)
        );
    }

    /// <summary>
    /// Whether the instant is at least (or, strictly, after) the current one plus
    /// the months, as PostgreSQL adds months (the day clamped to the end of the
    /// month), where a sum outside .NET's range is beyond every instant.
    /// </summary>
    private static bool AtLeastMonths(
        DateTime row,
        DateTime current,
        int months,
        bool strictly = false
    )
    {
        DateTime bound;
        try
        {
            bound = current.AddMonths(months);
        }
        catch (ArgumentOutOfRangeException)
        {
            return months < 0;
        }
        return strictly ? row > bound : row >= bound;
    }

    /// <summary>
    /// The values of the <c>Values</c> table as their types.
    /// </summary>
    private IQueryable<WindowItem> Items(ExecutionTables db)
    {
        return db.Values.Select(x => new WindowItem(
            long.Parse(x.Cells[ValueId].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                ? null
                : bool.Parse(x.Cells[Flag].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                ? null
                : int.Parse(x.Cells[Number].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                ? null
                : long.Parse(x.Cells[Big].Value.TextValue),
            x.Cells[Text].Value.TextValue,
            string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                ? null
                :
                    DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
        ));
    }

    /// <summary>
    /// The window over the items, translated, returns for each row what the window
    /// computed in memory over the enumerated items returns.
    /// </summary>
    private void AssertWindow<TResult>(
        Expression<Func<WindowItem, object?>>? partitionBy,
        Expression<Func<IEnumerable<WindowItem>, IOrderedEnumerable<WindowItem>>>? orderBy,
        Func<WindowItem, object?> peerKey,
        PostgreSqlFrame? frame,
        Expression<Func<WindowItem, PostgreSqlWindow<WindowItem>, TResult>> selector,
        Func<WindowItem, InMemoryWindow<WindowItem>, TResult> expected
    )
    {
        Func<WindowItem, object?> partition = partitionBy?.Compile() ?? (_ => null);
        Func<IEnumerable<WindowItem>, IOrderedEnumerable<WindowItem>>? order =
            orderBy?.Compile();
        Enumeration.AssertSameElements(
            db => MarkerCalls.Over(Items(db), partitionBy, orderBy, frame, selector),
            db =>
                InMemoryWindow<WindowItem>
                    .Over(Items(db), partition, order, peerKey)
                    .Select(x => expected(x.Row, x.Window))
        );
    }

    /// <summary>
    /// The calls of PostgreSQL markers in a tree.
    /// </summary>
    private sealed class MarkerCallFinder : ExpressionVisitor
    {
        private readonly List<MethodCallExpression> _calls = [];

        public IReadOnlyList<MethodCallExpression> In(Expression expression)
        {
            _ = Visit(expression);
            return _calls;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryElement.IsMarker(node))
            {
                _calls.Add(node);
            }
            return base.VisitMethodCall(node);
        }
    }
}
