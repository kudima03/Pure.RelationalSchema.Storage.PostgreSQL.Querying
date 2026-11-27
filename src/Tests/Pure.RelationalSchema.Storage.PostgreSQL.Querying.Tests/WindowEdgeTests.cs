#pragma warning disable xUnit1004 // Tests blocked on a fix are skipped with the reason.

using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Windows of <c>PostgreSqlWindows.Over</c> over the edges the window tests leave
/// out: partitions and orders by keys of every column type (uuid, unsigned numbers,
/// dates, times, doubles, the text of instants and of hashes, NULLs first ascending
/// and last descending), in-memory keys PostgreSQL orders unlike .NET (NaN, -0,
/// surrogate pairs, decimal scales), frames with offsets beyond the partition and
/// of peers only, <c>RANGE</c> frames of intervals over dates and times, the
/// functions at the edges of their partitions and aggregates of frames of NULLs,
/// each compared with the window computed in memory over the enumerated rows.
/// </summary>
public sealed record WindowEdgeTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public WindowEdgeTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private WindowEdgeSources Sources => new WindowEdgeSources(_fixture);

    [Fact]
    public void RanksByAUuidWithNullFirst()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Code),
            x => x.Code,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void RanksByAUuidDescendingWithNullLast()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderByDescending(x => x.Code),
            x => x.Code,
            null,
            (x, w) => new ValueTuple<long, long, double>(x.Id, w.Rank(), w.CumeDist()),
            (x, w) => new ValueTuple<long, long, double>(x.Id, w.Rank, w.CumeDist)
        );
    }

    [Fact]
    public void PartitionsByAUuidAndNumbersItsRows()
    {
        AssertItems(
            x => x.Code,
            rows => rows.OrderByDescending(x => x.Id),
            x => x.Id,
            null,
            (x, w) =>
                new ValueTuple<long, long, int>(x.Id, w.RowNumber(), w.Rows.Count()),
            (x, w) => new ValueTuple<long, long, int>(x.Id, w.RowNumber, w.Peers.Count())
        );
    }

    [Fact]
    public void RanksByAnUnsignedLongBeyondTheSignedRange()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Huge),
            x => x.Huge,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void RanksByAnUnsignedLongDescendingThenByAnUnsignedShort()
    {
        AssertItems(
            null,
            rows => rows.OrderByDescending(x => x.Huge).ThenBy(x => x.Small),
            x => (x.Huge, x.Small),
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void PartitionsByAnUnsignedShortAndReadsTheLastValue()
    {
        AssertItems(
            x => x.Small,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(null, null),
            (x, w) =>
                new ValueTuple<long, long, ulong?>(
                    x.Id,
                    w.LastValue(row => row.Id),
                    w.FirstValue(row => row.Huge)
                ),
            (x, w) =>
                new ValueTuple<long, long, ulong?>(
                    x.Id,
                    w.Rows(null, null).Last().Id,
                    w.Rows(null, null).First().Huge
                )
        );
    }

    [Fact]
    public void RanksByAFlagThenByANumberDescendingWithNullsLast()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Flag).ThenByDescending(x => x.Number),
            x => (x.Flag, x.Number),
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.PercentRank()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.PercentRank
                )
        );
    }

    [Fact]
    public void NumbersRowsByThreeKeysMixingDirections()
    {
        AssertItems(
            x => x.Text,
            rows =>
                rows.OrderByDescending(x => x.Flag)
                    .ThenBy(x => x.Big)
                    .ThenByDescending(x => x.Id),
            x => (x.Flag, x.Big, x.Id),
            null,
            (x, w) =>
                new ValueTuple<long, long, long>(
                    x.Id,
                    w.RowNumber(),
                    w.Lag(row => row.Id, 1, 0L)
                ),
            (x, w) =>
                new ValueTuple<long, long, long>(
                    x.Id,
                    w.RowNumber,
                    w.Lag(row => row.Id, 1, 0L)
                )
        );
    }

    [Fact]
    public void RanksByTheTextOfInstantsInTheOrdinalOrder()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.MomentText, StringComparer.Ordinal),
            x => x.MomentText,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void PartitionsByTheTextOfInstantsHoldingFractions()
    {
        AssertItems(
            x => x.MomentText,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (x, w) =>
                new ValueTuple<long, long, long>(
                    x.Id,
                    w.RowNumber(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, long, long>(
                    x.Id,
                    w.RowNumber,
                    w.Peers.Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void RanksByTextInTheDefaultOrder()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Text),
            x => x.Text,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void RanksByTheTextOfAHashInTheOrdinalOrder()
    {
        AssertExtras(
            null,
            rows => rows.OrderByDescending(x => x.Digest, StringComparer.Ordinal),
            x => x.Digest,
            null,
            (x, w) =>
                new ValueTuple<long, long, string>(
                    x.Id,
                    w.Rank(),
                    w.Lag(row => row.Digest, 1, "none")
                ),
            (x, w) =>
                new ValueTuple<long, long, string>(
                    x.Id,
                    w.Rank,
                    w.Lag(row => row.Digest, 1, "none")
                )
        );
    }

    [Fact]
    public void RanksByADateDescendingWithNullLast()
    {
        AssertExtras(
            null,
            rows => rows.OrderByDescending(x => x.Day),
            x => x.Day,
            null,
            (x, w) =>
                new ValueTuple<long, long, DateOnly?>(
                    x.Id,
                    w.Rank(),
                    w.Lead(row => row.Day, 1, null)
                ),
            (x, w) =>
                new ValueTuple<long, long, DateOnly?>(
                    x.Id,
                    w.Rank,
                    w.Lead(row => row.Day, 1, null)
                )
        );
    }

    [Fact]
    public void RanksByATimeWithFractionsWithNullFirst()
    {
        AssertExtras(
            null,
            rows => rows.OrderBy(x => x.Clock),
            x => x.Clock,
            null,
            (x, w) =>
                new ValueTuple<long, long, TimeOnly?>(
                    x.Id,
                    w.Rank(),
                    w.Lag(row => row.Clock, 1, null)
                ),
            (x, w) =>
                new ValueTuple<long, long, TimeOnly?>(
                    x.Id,
                    w.Rank,
                    w.Lag(row => row.Clock, 1, null)
                )
        );
    }

    [Fact]
    public void RanksByADoubleDescendingAndSumsItsFrame()
    {
        AssertExtras(
            null,
            rows => rows.OrderByDescending(x => x.Measure),
            x => x.Measure,
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, long, double?, double?>(
                    x.Id,
                    w.Rank(),
                    w.Rows.Max(row => row.Measure),
                    w.Rows.Min(row => row.Measure)
                ),
            (x, w) =>
                new ValueTuple<long, long, double?, double?>(
                    x.Id,
                    w.Rank,
                    w.Rows(-1, 1).Max(row => row.Measure),
                    w.Rows(-1, 1).Min(row => row.Measure)
                )
        );
    }

    [Fact(
        Skip = "PENDING: translator orders a double ascending with NaN last, as "
            + "PostgreSQL does (NaN above every number), where .NET's order puts NaN "
            + "first (below negative infinity); ORDER BY of the window needs NaN handled"
    )]
    public void RanksInMemoryDoublesWithNaNInfinitiesAndNegativeZero()
    {
        (long Id, double Value)[] values =
        [
            (1, double.NaN),
            (2, 1.0),
            (3, double.NegativeInfinity),
            (4, double.PositiveInfinity),
            (5, -0.0),
            (6, 0.0),
        ];

        AssertSequence(
            values,
            null,
            rows => rows.OrderBy(x => x.Value),
            x => x.Value,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void RanksInMemoryDoublesWithNegativeZeroAsAPeerOfZero()
    {
        (long Id, double Value)[] values =
        [
            (1, 2.5),
            (2, -0.0),
            (3, 0.0),
            (4, -1.25),
            (5, 0.0),
        ];

        AssertSequence(
            values,
            null,
            rows => rows.OrderByDescending(x => x.Value),
            x => x.Value,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, int>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.Rows.Count()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, int>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.Peers.Count()
                )
        );
    }

    [Fact(
        Skip = "PENDING: translator sorts StringComparer.Ordinal with COLLATE \"C\", "
            + "by UTF-8 bytes (code points), where .NET's ordinal order compares UTF-16 "
            + "code units, so a surrogate pair sorts before U+E000..U+FFFF in .NET and "
            + "after them in PostgreSQL"
    )]
    public void NumbersInMemoryTextInTheOrdinalOrderOfUtf16CodeUnits()
    {
        (long Id, string Label)[] labels =
        [
            (1, "Ａ"),
            (2, "\U0001F600"),
            (3, "z"),
            (4, "é"),
            (5, ""),
        ];

        AssertSequence(
            labels,
            null,
            rows => rows.OrderBy(x => x.Label, StringComparer.Ordinal),
            x => x.Label,
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void NumbersInMemoryTextOfOneUnicodePlaneInTheOrdinalOrder()
    {
        (long Id, string Label)[] labels =
        [
            (1, "b"),
            (2, "B"),
            (3, "a"),
            (4, "Ä"),
            (5, ""),
            (6, "A"),
            (7, "a b"),
            (8, "ab"),
        ];

        AssertSequence(
            labels,
            null,
            rows => rows.OrderByDescending(x => x.Label, StringComparer.Ordinal),
            x => x.Label,
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber()),
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
        );
    }

    [Fact]
    public void RanksInMemoryDecimalsOfDifferentScalesAsPeers()
    {
        (long Id, decimal Value)[] values =
        [
            (1, 1.0m),
            (2, 1.00m),
            (3, 2m),
            (4, -0.5m),
            (5, 1m),
        ];

        AssertSequence(
            values,
            null,
            rows => rows.OrderBy(x => x.Value),
            x => x.Value,
            null,
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void PartitionsInMemoryDecimalsOfDifferentScalesTogether()
    {
        (long Id, decimal Value)[] values = [(1, 1.0m), (2, 1.00m), (3, 2m), (4, 1m)];

        AssertSequence(
            values,
            x => x.Value,
            null,
            x => null,
            null,
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Peers.Count(),
                    w.Peers.Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void AggregatesTheWholePartitionForOffsetsBeyondIt()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1000, 1000),
            (x, w) =>
                new ValueTuple<long, int, long, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id),
                    w.FirstValue(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long, long>(
                    x.Id,
                    w.Rows(-1000, 1000).Count(),
                    w.Rows(-1000, 1000).Sum(row => row.Id),
                    w.Rows(-1000, 1000).First().Id
                )
        );
    }

    [Fact]
    public void AggregatesNoRowsForAFrameStartingBeyondThePartition()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(100, null),
            (x, w) =>
                new ValueTuple<long, int, long?, bool, bool>(
                    x.Id,
                    w.Rows.Count(),
                    w.LastValue(row => (long?)row.Id),
                    w.Rows.Any(),
                    w.Rows.All(row => row.Id < 0)
                ),
            (x, w) =>
                new ValueTuple<long, int, long?, bool, bool>(
                    x.Id,
                    w.Rows(100, null).Count(),
                    w.Rows(100, null).Select(row => (long?)row.Id).LastOrDefault(),
                    w.Rows(100, null).Any(),
                    w.Rows(100, null).All(row => row.Id < 0)
                )
        );
    }

    [Fact]
    public void AggregatesThePeersOfTheRowInARangeOfTheCurrentRow()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Text, StringComparer.Ordinal).ThenBy(x => x.Flag),
            x => (x.Text, x.Flag),
            PostgreSqlFrame.Range(0m, 0m),
            (x, w) =>
                new ValueTuple<long, int, long, long, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id),
                    w.Rows.Min(row => row.Id),
                    w.Rows.Max(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long, long, long>(
                    x.Id,
                    PeersOnly(w).Count(),
                    PeersOnly(w).Sum(row => row.Id),
                    PeersOnly(w).Min(row => row.Id),
                    PeersOnly(w).Max(row => row.Id)
                )
        );
    }

    [Fact]
    public void AggregatesFromTheFirstPeerToTheEndInARange()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderByDescending(x => x.Number),
            x => x.Number,
            PostgreSqlFrame.Range(0m, null),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    FromFirstPeer(w).Count(),
                    FromFirstPeer(w).Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void AggregatesThePartitionInAnUnboundedRange()
    {
        AssertItems(
            x => x.Text,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Range((decimal?)null, null),
            (x, w) =>
                new ValueTuple<long, int, long, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.LastValue(row => row.Id),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long, long>(
                    x.Id,
                    w.Rows(null, null).Count(),
                    w.Rows(null, null).Last().Id,
                    w.Rows(null, null).Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void AggregatesTheDefaultFrameWrittenAsARange()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Number),
            x => x.Number,
            PostgreSqlFrame.Range(null, 0m),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Peers.Count(),
                    w.Peers.Sum(row => row.Id)
                )
        );
    }

    [Theory]
    [InlineData("-1", "1")]
    [InlineData("-3000000000", "0")]
    [InlineData("0", "4294967296")]
    [InlineData("-2147483648", "-1")]
    public void AggregatesARangeOfAnIntegerKeyAtItsLimits(string start, string end)
    {
        decimal from = decimal.Parse(
            start,
            System.Globalization.CultureInfo.InvariantCulture
        );
        decimal to = decimal.Parse(
            end,
            System.Globalization.CultureInfo.InvariantCulture
        );

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db).Where(x => x.Number != null),
                    null,
                    rows => rows.OrderBy(x => x.Number),
                    PostgreSqlFrame.Range(from, to),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db).Where(x => x.Number != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Number),
                        x => x.Number
                    )
                    .Select(x =>
                    {
                        EdgeItem[] frame =
                        [
                            .. x.Window.Range(
                                row => (decimal)row.Number!.Value,
                                (row, current) =>
                                    row >= current + from && row <= current + to
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
    [InlineData("-1", "1")]
    [InlineData("-0.5", "0.5")]
    [InlineData("0", "1000000000000000")]
    public void AggregatesARangeOfADoubleKey(string start, string end)
    {
        decimal from = decimal.Parse(
            start,
            System.Globalization.CultureInfo.InvariantCulture
        );
        decimal to = decimal.Parse(
            end,
            System.Globalization.CultureInfo.InvariantCulture
        );

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Measure != null),
                    null,
                    rows => rows.OrderBy(x => x.Measure),
                    PostgreSqlFrame.Range(from, to),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<ExtraItem>
                    .Over(
                        Sources.Extras(db).Where(x => x.Measure != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Measure),
                        x => x.Measure
                    )
                    .Select(x =>
                    {
                        ExtraItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Measure!.Value,
                                (row, current) =>
                                    row >= current + (double)from
                                    && row <= current + (double)to
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
    [InlineData(-1, 1)]
    [InlineData(-2, 0)]
    [InlineData(0, 400000)]
    [InlineData(-0.5, 0)]
    public void AggregatesARangeOfDaysOverADate(double startDays, double endDays)
    {
        TimeSpan start = TimeSpan.FromDays(startDays);
        TimeSpan end = TimeSpan.FromDays(endDays);

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Day != null),
                    null,
                    rows => rows.OrderBy(x => x.Day),
                    PostgreSqlFrame.Range(start, end),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<ExtraItem>
                    .Over(
                        Sources.Extras(db).Where(x => x.Day != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Day),
                        x => x.Day
                    )
                    .Select(x =>
                    {
                        ExtraItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Day!.Value.ToDateTime(TimeOnly.MinValue),
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
    [InlineData(-1, 0)]
    [InlineData(0, 12)]
    [InlineData(-12000, 1)]
    public void AggregatesARangeOfMonthsOverADate(int start, int end)
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Day != null),
                    null,
                    rows => rows.OrderByDescending(x => x.Day),
                    PostgreSqlFrame.RangeMonths(start, end),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<ExtraItem>
                    .Over(
                        Sources.Extras(db).Where(x => x.Day != null),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Day),
                        x => x.Day
                    )
                    .Select(x =>
                    {
                        ExtraItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Day!.Value,
                                (row, current) =>
                                    WithinDescendingMonths(row, current, start, end)
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
    [InlineData(-3600, 3600)]
    [InlineData(-0.5, 0)]
    [InlineData(0, 0.000001)]
    [InlineData(-86400, 0)]
    [InlineData(0, 43200)]
    public void AggregatesARangeOfSecondsOverATimeWithoutWrappingAroundMidnight(
        double startSeconds,
        double endSeconds
    )
    {
        TimeSpan start = TimeSpan.FromTicks((long)Math.Round(startSeconds * 10_000_000));
        TimeSpan end = TimeSpan.FromTicks((long)Math.Round(endSeconds * 10_000_000));

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Clock != null),
                    null,
                    rows => rows.OrderBy(x => x.Clock),
                    PostgreSqlFrame.Range(start, end),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<ExtraItem>
                    .Over(
                        Sources.Extras(db).Where(x => x.Clock != null),
                        x => null,
                        rows => rows.OrderBy(x => x.Clock),
                        x => x.Clock
                    )
                    .Select(x =>
                    {
                        ExtraItem[] frame =
                        [
                            .. x.Window.Range(
                                row => row.Clock!.Value.Ticks,
                                (row, current) =>
                                    row - current >= start.Ticks
                                    && row - current <= end.Ticks
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
    public void ReadsTheOneRowOfEachPartitionAtTheEdgesOfTheRanking()
    {
        AssertItems(
            x => x.Id,
            rows => rows.OrderBy(x => x.Number),
            x => x.Number,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double, double, int>(
                    x.Id,
                    w.RowNumber(),
                    w.Rank(),
                    w.PercentRank(),
                    w.CumeDist(),
                    w.Ntile(5)
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double, double, int>(
                    x.Id,
                    w.RowNumber,
                    w.Rank,
                    w.PercentRank,
                    w.CumeDist,
                    w.Ntile(5)
                )
        );
    }

    [Fact]
    public void RanksEveryRowOfAPartitionOfPeersFirst()
    {
        AssertItems(
            x => x.Text,
            rows => rows.OrderBy(x => x.Text, StringComparer.Ordinal),
            x => x.Text,
            null,
            (x, w) =>
                new ValueTuple<long, long, double, double, int>(
                    x.Id,
                    w.DenseRank(),
                    w.PercentRank(),
                    w.CumeDist(),
                    w.Rows.Count()
                ),
            (x, w) =>
                new ValueTuple<long, long, double, double, int>(
                    x.Id,
                    w.DenseRank,
                    w.PercentRank,
                    w.CumeDist,
                    w.Peers.Count()
                )
        );
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(14)]
    public void DividesOnePartitionIntoBucketsOfUnevenSizes(int buckets)
    {
        AssertItems(
            null,
            rows => rows.OrderByDescending(x => x.Id),
            x => x.Id,
            null,
            (x, w) => new ValueTuple<long, int>(x.Id, w.Ntile(buckets)),
            (x, w) => new ValueTuple<long, int>(x.Id, w.Ntile(buckets))
        );
    }

    [Fact]
    public void ReadsTheRowAnOffsetOfTheCurrentRowAway()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (current, w) =>
                new ValueTuple<long, long, long>(
                    current.Id,
                    w.Lag(x => x.Id, (int)(current.Id % 3), -1L),
                    w.Lead(x => x.Id, (int)(current.Id % 2) + 1, current.Id * -1)
                ),
            (current, w) =>
                new ValueTuple<long, long, long>(
                    current.Id,
                    w.Lag(x => x.Id, (int)(current.Id % 3), -1L),
                    w.Lead(x => x.Id, (int)(current.Id % 2) + 1, current.Id * -1)
                )
        );
    }

    [Fact]
    public void ReadsANullValueOfAnExistingRowRatherThanTheDefault()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (current, w) =>
                new ValueTuple<long, int?, Guid?, ushort?>(
                    current.Id,
                    w.Lag(x => x.Number, 1, -5),
                    w.Lead(x => x.Code, 1, Guid.Empty),
                    w.Lag(x => x.Small, 2, (ushort)9)
                ),
            (current, w) =>
                new ValueTuple<long, int?, Guid?, ushort?>(
                    current.Id,
                    w.Lag(x => x.Number, 1, -5),
                    w.Lead(x => x.Code, 1, Guid.Empty),
                    w.Lag(x => x.Small, 2, (ushort)9)
                )
        );
    }

    [Fact]
    public void ReadsTheFirstAndLastValuesOfPeersOfNullKeys()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Flag).ThenBy(x => x.Id),
            x => (x.Flag, x.Id),
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, bool?, bool?, string>(
                    x.Id,
                    w.FirstValue(row => row.Flag),
                    w.LastValue(row => row.Flag),
                    w.FirstValue(row => row.MomentText)
                ),
            (x, w) =>
                new ValueTuple<long, bool?, bool?, string>(
                    x.Id,
                    w.Rows(-1, 1).First().Flag,
                    w.Rows(-1, 1).Last().Flag,
                    w.Rows(-1, 1).First().MomentText
                )
        );
    }

    [Fact]
    public void CountsTheRowsOfAFrameWhoseNullableTestIsFalseOrNull()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-2, 2),
            (x, w) =>
                new ValueTuple<long, int, int, int, long>(
                    x.Id,
                    w.Rows.Count(row => !(row.Number > 0)),
                    w.Rows.Count(row => row.Number != 0),
                    w.Rows.Count(row => row.Flag != true),
                    w.Rows.Where(row => row.Flag != false).Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, int, int, long>(
                    x.Id,
                    w.Rows(-2, 2).Count(row => !(row.Number > 0)),
                    w.Rows(-2, 2).Count(row => row.Number != 0),
                    w.Rows(-2, 2).Count(row => row.Flag != true),
                    w.Rows(-2, 2).Where(row => row.Flag != false).Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void TestsTheNullableValuesOfAFrameAsDotNetDoes()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 0),
            (x, w) =>
                new ValueTuple<long, bool, bool, bool>(
                    x.Id,
                    w.Rows.Any(row => row.Number != 5),
                    w.Rows.All(row => row.Number == null),
                    w.Rows.Any(row => !(row.Flag == true))
                ),
            (x, w) =>
                new ValueTuple<long, bool, bool, bool>(
                    x.Id,
                    w.Rows(-1, 0).Any(row => row.Number != 5),
                    w.Rows(-1, 0).All(row => row.Number == null),
                    w.Rows(-1, 0).Any(row => !(row.Flag == true))
                )
        );
    }

    [Fact]
    public void AggregatesFramesOfOnlyNullValues()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, long?, double?, int?, long?, int>(
                    x.Id,
                    w.Rows.Sum(row => (long?)row.Number),
                    w.Rows.Average(row => row.Number),
                    w.Rows.Max(row => row.Number),
                    w.Rows.Min(row => row.Big),
                    w.Rows.Count(row => row.Code == null)
                ),
            (x, w) =>
                new ValueTuple<long, long?, double?, int?, long?, int>(
                    x.Id,
                    w.Rows(-1, 1).Sum(row => (long?)row.Number),
                    w.Rows(-1, 1).Average(row => row.Number),
                    w.Rows(-1, 1).Max(row => row.Number),
                    w.Rows(-1, 1).Min(row => row.Big),
                    w.Rows(-1, 1).Count(row => row.Code == null)
                )
        );
    }

    [Fact]
    public void AggregatesNullableDoublesDatesAndTimesOfAFrame()
    {
        AssertExtras(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, double?, double?, DateOnly?, TimeOnly?>(
                    x.Id,
                    w.Rows.Sum(row => row.Measure),
                    w.Rows.Average(row => row.Measure),
                    w.Rows.Max(row => row.Day),
                    w.Rows.Min(row => row.Clock)
                ),
            (x, w) =>
                new ValueTuple<long, double?, double?, DateOnly?, TimeOnly?>(
                    x.Id,
                    w.Rows(-1, 1).Sum(row => row.Measure),
                    w.Rows(-1, 1).Average(row => row.Measure),
                    w.Rows(-1, 1).Max(row => row.Day),
                    w.Rows(-1, 1).Min(row => row.Clock)
                )
        );
    }

    [Fact]
    public void AggregatesLongsAndUnsignedNumbersAtTheirLimitsInAFrame()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 0),
            (x, w) =>
                new ValueTuple<long, long?, long?, long?, ulong?, ushort?>(
                    x.Id,
                    w.Rows.Sum(row => row.Big),
                    w.Rows.Min(row => row.Big),
                    w.Rows.Max(row => row.Big),
                    w.Rows.Max(row => row.Huge),
                    w.Rows.Min(row => row.Small)
                ),
            (x, w) =>
                new ValueTuple<long, long?, long?, long?, ulong?, ushort?>(
                    x.Id,
                    w.Rows(-1, 0).Sum(row => row.Big),
                    w.Rows(-1, 0).Min(row => row.Big),
                    w.Rows(-1, 0).Max(row => row.Big),
                    w.Rows(-1, 0).Max(row => row.Huge),
                    w.Rows(-1, 0).Min(row => row.Small)
                )
        );
    }

    [Fact(
        Skip = "PENDING: translator writes Average of long as AVG(CAST(x AS double "
            + "precision)), rounding each value before summing, where .NET sums the "
            + "longs exactly and divides once: the frame of long.MinValue and "
            + "long.MaxValue averages 0 instead of -0.5"
    )]
    public void AveragesLongsAtTheirLimitsExactlyInAFrame()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 0),
            (x, w) => new ValueTuple<long, double?>(x.Id, w.Rows.Average(row => row.Big)),
            (x, w) =>
                new ValueTuple<long, double?>(x.Id, w.Rows(-1, 0).Average(row => row.Big))
        );
    }

    [Fact]
    public void ThrowsForTheAverageOfAnEmptyFrameOfValues()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(1, 2),
            (x, w) => new ValueTuple<long, double>(x.Id, w.Rows.Average(row => row.Id)),
            (x, w) =>
                new ValueTuple<long, double>(x.Id, w.Rows(1, 2).Average(row => row.Id))
        );
    }

    [Fact]
    public void ReadsTheLastValueOfAFrameEndingAtTheCurrentRowOfANullKey()
    {
        AssertItems(
            x => x.Flag,
            rows => rows.OrderByDescending(x => x.Code).ThenBy(x => x.Id),
            x => (x.Code, x.Id),
            PostgreSqlFrame.Rows(null, 0),
            (x, w) =>
                new ValueTuple<long, Guid?, long>(
                    x.Id,
                    w.LastValue(row => row.Code),
                    w.FirstValue(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, Guid?, long>(
                    x.Id,
                    w.Rows(null, 0).Last().Code,
                    w.Rows(null, 0).First().Id
                )
        );
    }

    [Fact(
        Skip = "PENDING: EntityExpression binds the lambda parameters of Over itself "
            + "only, not those of the window functions (Lag, FirstValue) nor of the "
            + "Enumerable operators over w.Rows, so row.Cells.Count there throws "
            + "NotSupportedException in EntityExpression.Columns"
    )]
    public void ReadsTheCountOfTheCellsOfTheRowsOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    PostgreSqlFrame.Rows(-1, 0),
                    (current, w) =>
                        new ValueTuple<string, int, int, int>(
                            current.Cells[ValueId].Value.TextValue,
                            w.Lag(x => x.Cells.Count, 1, -1),
                            w.FirstValue(x => x.Cells.Count),
                            w.Rows.Sum(x => x.Cells.Count)
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, int, int, int>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.Lag(row => row.Cells.Count, 1, -1),
                        x.Window.Rows(-1, 0).First().Cells.Count,
                        x.Window.Rows(-1, 0).Sum(row => row.Cells.Count)
                    ))
        );
    }

    [Fact(
        Skip = "PENDING: EntityExpression does not bind the parameter of the key "
            + "selector of OrderBy over the rows of a window (an Enumerable call), so "
            + "row.Cells.Count there throws NotSupportedException in "
            + "EntityExpression.Columns"
    )]
    public void OrdersAWindowByTheCountOfTheCellsOfItsRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x => x.Cells.Count,
                    rows =>
                        rows.OrderBy(x => x.Cells.Count)
                            .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (current, w) =>
                        new ValueTuple<string, long>(
                            current.Cells[ValueId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => x.Cells.Count,
                        rows =>
                            rows.OrderBy(x => x.Cells.Count)
                                .ThenBy(x =>
                                    long.Parse(x.Cells[ValueId].Value.TextValue)
                                ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, long>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void PartitionsByTheCountOfTheCellsOfTheRow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x => x.Cells.Count,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (current, w) =>
                        new ValueTuple<string, long, int>(
                            current.Cells[ValueId].Value.TextValue,
                            w.RowNumber(),
                            current.Cells.Count
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => x.Cells.Count,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, long, int>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.RowNumber,
                        x.Row.Cells.Count
                    ))
        );
    }

    [Fact]
    public void ReadsTheTextOfTheRowsOfAWindowThroughTheirCells()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x => x.Cells[Text].Value.TextValue,
                    rows =>
                        rows.OrderByDescending(
                                x => x.Cells[_fixture.Moment].Value.TextValue,
                                StringComparer.Ordinal
                            )
                            .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (current, w) =>
                        new ValueTuple<string, string, string>(
                            current.Cells[ValueId].Value.TextValue,
                            w.Lead(x => x.Cells[_fixture.Code].Value.TextValue, 1, "-"),
                            w.FirstValue(x => x.Cells[_fixture.Huge].Value.TextValue)
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => x.Cells[Text].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(
                                    x => x.Cells[_fixture.Moment].Value.TextValue,
                                    StringComparer.Ordinal
                                )
                                .ThenBy(x =>
                                    long.Parse(x.Cells[ValueId].Value.TextValue)
                                ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, string, string>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.Lead(
                            row => row.Cells[_fixture.Code].Value.TextValue,
                            1,
                            "-"
                        ),
                        x.Window.Peers.First().Cells[_fixture.Huge].Value.TextValue
                    ))
        );
    }

    [Fact(
        Skip = "PENDING: translator writes the double -0.0 as the integer literal -0, "
            + "in VALUES and as a constant (here the default of LAG), so PostgreSQL "
            + "reads 0 and the sign of the zero is lost"
    )]
    public void ReadsTheNegativeZeroOfInMemoryDoublesThroughAWindow()
    {
        (long Id, double Value)[] values = [(1, 2.5), (2, -0.0), (3, 0.0)];

        AssertSequence(
            values,
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            null,
            (x, w) =>
                new ValueTuple<long, double, double>(
                    x.Id,
                    x.Value,
                    w.Lag(row => row.Value, 1, -0.0)
                ),
            (x, w) =>
                new ValueTuple<long, double, double>(
                    x.Id,
                    x.Value,
                    w.Lag(row => row.Value, 1, -0.0)
                )
        );
    }

    [Fact]
    public void DropsConstantKeysOfThePartitionAndTheOrder()
    {
        int constant = 1;

        AssertItems(
            x => new ValueTuple<int, bool?>(constant, x.Flag),
            rows => rows.OrderBy(x => constant).ThenBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Range(-2m, 0m),
            (x, w) =>
                new ValueTuple<long, long, int, long>(
                    x.Id,
                    w.Rank(),
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, long, int, long>(
                    x.Id,
                    w.Rank,
                    w.Range(
                            row => row.Id,
                            (row, current) => row >= current - 2 && row <= current
                        )
                        .Count(),
                    w.Range(
                            row => row.Id,
                            (row, current) => row >= current - 2 && row <= current
                        )
                        .Sum(row => row.Id)
                )
        );
    }

    [Fact]
    public void CountsTheProjectedAndFilteredValuesOfAFrameHoldingNulls()
    {
        AssertItems(
            null,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, int, long, int, int?>(
                    x.Id,
#pragma warning disable IDE0120 // The aggregates of a projection are the tree tested.
                    w.Rows.Select(row => row.Number).Count(),
                    w.Rows.Select(row => row.Code).LongCount(),
                    w.Rows.Select(row => row.Number).Where(n => n != null).Count(),
                    w.Rows.Select(row => row.Number).Where(n => n < 1).Max()
#pragma warning restore IDE0120
                ),
            (x, w) =>
                new ValueTuple<long, int, long, int, int?>(
                    x.Id,
                    w.Rows(-1, 1).Count(),
                    w.Rows(-1, 1).LongCount(),
                    w.Rows(-1, 1).Count(row => row.Number != null),
                    w.Rows(-1, 1).Where(row => row.Number < 1).Max(row => row.Number)
                )
        );
    }

    [Fact]
    public void RanksAnInMemorySequenceOfScalarsWithTies()
    {
        long[] values = [3, 1, 2, 3, -5, 1, 3];

        AssertSequence(
            values,
            x => x % 2 == 0,
            rows => rows.OrderByDescending(x => x),
            x => x,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, double, int>(
                    x,
                    w.Rank(),
                    w.DenseRank(),
                    w.CumeDist(),
                    w.Rows.Count()
                ),
            (x, w) =>
                new ValueTuple<long, long, long, double, int>(
                    x,
                    w.Rank,
                    w.DenseRank,
                    w.CumeDist,
                    w.Peers.Count()
                )
        );
    }

    [Fact]
    public void RanksInMemoryTextHoldingNullAndEmptyText()
    {
        (long Id, string? Label)[] labels =
        [
            (1, "b"),
            (2, null),
            (3, ""),
            (4, "a"),
            (5, null),
            (6, ""),
        ];

        AssertSequence(
            labels,
            null,
            rows => rows.OrderBy(x => x.Label, StringComparer.Ordinal),
            x => x.Label,
            null,
            (x, w) =>
                new ValueTuple<long, long, long, string?>(
                    x.Id,
                    w.Rank(),
                    w.DenseRank(),
                    w.Lag(row => row.Label, 2, "none")
                ),
            (x, w) =>
                new ValueTuple<long, long, long, string?>(
                    x.Id,
                    w.Rank,
                    w.DenseRank,
                    w.Lag(row => row.Label, 2, "none")
                )
        );
    }

    [Fact]
    public void ReadsInMemoryInstantsWithFractionsInOtherRowsOfAWindow()
    {
        (long Id, DateTime Moment)[] moments =
        [
            (1, new DateTime(2024, 1, 1, 0, 0, 0).AddTicks(1234560)),
            (2, DateTime.MinValue),
            (3, new DateTime(9999, 12, 31, 23, 59, 59).AddTicks(9999990)),
            (4, new DateTime(2024, 1, 1, 0, 0, 0)),
            (5, new DateTime(1970, 1, 1, 0, 0, 0).AddTicks(10)),
        ];

        AssertSequence(
            moments,
            null,
            rows => rows.OrderBy(x => x.Moment),
            x => x.Moment,
            null,
            (x, w) =>
                new ValueTuple<long, long, DateTime, DateTime>(
                    x.Id,
                    w.RowNumber(),
                    w.Lag(row => row.Moment, 1, DateTime.MaxValue.AddTicks(-9)),
                    w.FirstValue(row => row.Moment)
                ),
            (x, w) =>
                new ValueTuple<long, long, DateTime, DateTime>(
                    x.Id,
                    w.RowNumber,
                    w.Lag(row => row.Moment, 1, DateTime.MaxValue.AddTicks(-9)),
                    w.Peers.First().Moment
                )
        );
    }

    [Fact]
    public void RanksInMemoryUuidsDifferingInEveryGroup()
    {
        (long Id, Guid Code)[] codes =
        [
            (1, new Guid("80000000-0000-0000-0000-000000000000")),
            (2, new Guid("7fffffff-ffff-ffff-ffff-ffffffffffff")),
            (3, new Guid("00000000-8000-0000-0000-000000000000")),
            (4, new Guid("00000000-0000-8000-0000-000000000000")),
            (5, new Guid("00000000-0000-0000-8000-000000000000")),
            (6, new Guid("00000000-0000-0000-0000-800000000000")),
            (7, new Guid("00000000-0000-0000-0000-000000000001")),
            (8, new Guid("00000000-7fff-0000-0000-000000000000")),
        ];

        AssertSequence(
            codes,
            null,
            rows => rows.OrderBy(x => x.Code),
            x => x.Code,
            null,
            (x, w) =>
                new ValueTuple<long, long, Guid>(
                    x.Id,
                    w.RowNumber(),
                    w.Lead(row => row.Code, 1, Guid.Empty)
                ),
            (x, w) =>
                new ValueTuple<long, long, Guid>(
                    x.Id,
                    w.RowNumber,
                    w.Lead(row => row.Code, 1, Guid.Empty)
                )
        );
    }

    [Theory]
    [InlineData(-3600, 0)]
    [InlineData(-0.5, 0.5)]
    [InlineData(0, 86400)]
    public void AggregatesARangeOfSecondsOverInMemoryDurations(
        double startSeconds,
        double endSeconds
    )
    {
        TimeSpan start = TimeSpan.FromTicks((long)Math.Round(startSeconds * 10_000_000));
        TimeSpan end = TimeSpan.FromTicks((long)Math.Round(endSeconds * 10_000_000));
        (long Id, TimeSpan Span)[] spans =
        [
            (1, TimeSpan.FromHours(-1)),
            (2, TimeSpan.Zero),
            (3, TimeSpan.FromMilliseconds(500)),
            (4, TimeSpan.FromDays(1)),
            (5, TimeSpan.FromHours(24)),
            (6, TimeSpan.FromTicks(-10)),
            (7, TimeSpan.FromDays(400).Add(TimeSpan.FromTicks(30))),
        ];

        AssertSequence(
            spans,
            null,
            rows => rows.OrderBy(x => x.Span),
            x => x.Span,
            PostgreSqlFrame.Range(start, end),
            (x, w) =>
                new ValueTuple<long, int, long, TimeSpan?>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id),
                    w.Lag(row => (TimeSpan?)row.Span, 1, null)
                ),
            (x, w) =>
            {
                (long Id, TimeSpan Span)[] frame =
                [
                    .. w.Range(
                        row => row.Span,
                        (row, current) => row - current >= start && row - current <= end
                    ),
                ];
                return new ValueTuple<long, int, long, TimeSpan?>(
                    x.Id,
                    frame.Length,
                    frame.Sum(row => row.Id),
                    w.Lag(row => (TimeSpan?)row.Span, 1, null)
                );
            }
        );
    }

    [Fact]
    public void AggregatesARangeOfMonthsOverInMemoryDates()
    {
        (long Id, DateOnly Day)[] days =
        [
            (1, new DateOnly(2024, 1, 31)),
            (2, new DateOnly(2024, 2, 29)),
            (3, new DateOnly(2024, 3, 31)),
            (4, new DateOnly(2023, 2, 28)),
            (5, new DateOnly(2024, 2, 28)),
            (6, new DateOnly(2024, 3, 1)),
        ];

        AssertSequence(
            days,
            null,
            rows => rows.OrderBy(x => x.Day),
            x => x.Day,
            PostgreSqlFrame.RangeMonths(-1, 0),
            (x, w) =>
                new ValueTuple<long, int, long, DateOnly>(
                    x.Id,
                    w.Rows.Count(),
                    w.Rows.Sum(row => row.Id),
                    w.FirstValue(row => row.Day)
                ),
            (x, w) =>
            {
                (long Id, DateOnly Day)[] frame =
                [
                    .. w.Range(
                        row => row.Day,
                        (row, current) => row >= current.AddMonths(-1) && row <= current
                    ),
                ];
                return new ValueTuple<long, int, long, DateOnly>(
                    x.Id,
                    frame.Length,
                    frame.Sum(row => row.Id),
                    frame[0].Day
                );
            }
        );
    }

    /// <summary>
    /// The peers of the row: the rows of its partition equal to it on every key.
    /// </summary>
    private static IEnumerable<EdgeItem> PeersOnly(InMemoryWindow<EdgeItem> window)
    {
        return window.Rows(window.Rank - window.RowNumber, null).Take(PeerCount(window));
    }

    private static int PeerCount(InMemoryWindow<EdgeItem> window)
    {
        return window.Peers.Count() - (int)window.Rank + 1;
    }

    /// <summary>
    /// The rows from the first peer of the row to the end of its partition.
    /// </summary>
    private static IEnumerable<EdgeItem> FromFirstPeer(InMemoryWindow<EdgeItem> window)
    {
        return window.Rows(window.Rank - window.RowNumber, null);
    }

    /// <summary>
    /// Whether the date is within the months of the current one in a descending
    /// order, where PRECEDING adds and FOLLOWING subtracts: a negative start of
    /// <paramref name="start"/> months is a date up to that many months later.
    /// </summary>
    private static bool WithinDescendingMonths(
        DateOnly row,
        DateOnly current,
        int start,
        int end
    )
    {
        return row <= Shifted(current, -start, true)
            && row >= Shifted(current, -end, false);
    }

    /// <summary>
    /// The date the months away, as PostgreSQL adds months (the day clamped to the end
    /// of the month), past .NET's range the last or first date.
    /// </summary>
    private static DateOnly Shifted(DateOnly date, int months, bool upper)
    {
        try
        {
            return date.AddMonths(months);
        }
        catch (ArgumentOutOfRangeException)
        {
            return upper ? DateOnly.MaxValue : DateOnly.MinValue;
        }
    }

    private void AssertItems<TResult>(
        Expression<Func<EdgeItem, object?>>? partitionBy,
        Expression<Func<IEnumerable<EdgeItem>, IOrderedEnumerable<EdgeItem>>>? orderBy,
        Func<EdgeItem, object?> peerKey,
        PostgreSqlFrame? frame,
        Expression<Func<EdgeItem, PostgreSqlWindow<EdgeItem>, TResult>> selector,
        Func<EdgeItem, InMemoryWindow<EdgeItem>, TResult> expected
    )
    {
        AssertWindow(
            Sources.Items,
            partitionBy,
            orderBy,
            peerKey,
            frame,
            selector,
            expected
        );
    }

    private void AssertExtras<TResult>(
        Expression<Func<ExtraItem, object?>>? partitionBy,
        Expression<Func<IEnumerable<ExtraItem>, IOrderedEnumerable<ExtraItem>>>? orderBy,
        Func<ExtraItem, object?> peerKey,
        PostgreSqlFrame? frame,
        Expression<Func<ExtraItem, PostgreSqlWindow<ExtraItem>, TResult>> selector,
        Func<ExtraItem, InMemoryWindow<ExtraItem>, TResult> expected
    )
    {
        AssertWindow(
            Sources.Extras,
            partitionBy,
            orderBy,
            peerKey,
            frame,
            selector,
            expected
        );
    }

    private void AssertSequence<TSource, TResult>(
        TSource[] sequence,
        Expression<Func<TSource, object?>>? partitionBy,
        Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>? orderBy,
        Func<TSource, object?> peerKey,
        PostgreSqlFrame? frame,
        Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>> selector,
        Func<TSource, InMemoryWindow<TSource>, TResult> expected
    )
    {
        AssertWindow(
            db => sequence.AsQueryable().Where(x => db.Parents.Any()),
            partitionBy,
            orderBy,
            peerKey,
            frame,
            selector,
            expected
        );
    }

    /// <summary>
    /// The window over the source, translated, returns for each row what the window
    /// computed in memory over the enumerated source returns.
    /// </summary>
    private void AssertWindow<TSource, TResult>(
        Func<ExecutionTables, IQueryable<TSource>> source,
        Expression<Func<TSource, object?>>? partitionBy,
        Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>? orderBy,
        Func<TSource, object?> peerKey,
        PostgreSqlFrame? frame,
        Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>> selector,
        Func<TSource, InMemoryWindow<TSource>, TResult> expected
    )
    {
        Func<TSource, object?> partition = partitionBy?.Compile() ?? (_ => null);
        Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>? order =
            orderBy?.Compile();
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    source(db),
                    partitionBy,
                    orderBy,
                    frame,
                    selector
                ),
            db =>
                InMemoryWindow<TSource>
                    .Over(source(db), partition, order, peerKey)
                    .Select(x => expected(x.Row, x.Window))
        );
    }
}
