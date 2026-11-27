#pragma warning disable xUnit1004 // Tests blocked on a fix are skipped with the reason.

using System.Globalization;
using Linq.Expressions.Translation.PostgreSql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Frames of <c>PostgreSqlWindows.Over</c> over stored tables in the shapes the
/// translator's issue #128 lists as defects: the extremes of strings over a frame
/// that may be empty, a frame filter reading an aggregate of a group, <c>RANGE</c>
/// offsets over integer keys that are fractions or lie beyond <c>bigint</c>, and
/// frame bounds at the limits of their types. Each is compared with the frame
/// computed in memory over the enumerated rows, in one statement.
/// </summary>
public sealed record FrameDefectTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public FrameDefectTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private WindowEdgeSources Sources => new WindowEdgeSources(_fixture);

    [Fact(
        Skip = "#111 (translator #128): Min and Max of a non-null string over a frame "
            + "that may be empty are taken as never null and get no COALESCE: '#' || "
            + "MAX(...) is NULL (null, not \"#\") and MIN(...) <> 'plain' is NULL, "
            + "which reading a bool refuses (InvalidOperationException)"
    )]
    public void ComposesTheExtremeTextOfAFrameThatMayBeEmpty()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    PostgreSqlFrame.Rows(-1, -1),
                    (x, w) =>
                        new ValueTuple<long, string, bool, bool>(
                            x.Id,
                            "#" + w.Rows.Max(y => y.Text),
                            w.Rows.Min(y => y.Text) != "plain",
                            w.Rows.Max(y => y.Text) == null
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, string, bool, bool>(
                        x.Row.Id,
                        "#" + x.Window.Rows(-1, -1).Max(y => y.Text),
                        x.Window.Rows(-1, -1).Min(y => y.Text) != "plain",
                        x.Window.Rows(-1, -1).Max(y => y.Text) == null
                    ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): a frame filter reading an aggregate of a group "
            + "puts the aggregate in FILTER (42803: aggregate functions are not "
            + "allowed in FILTER)"
    )]
    public void FiltersTheGroupsOfAFrameByTheirAggregates()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db).GroupBy(x => x.Flag),
                    null,
                    rows => rows.OrderBy(g => g.Key),
                    PostgreSqlFrame.Rows(-1, 0),
                    (g, w) =>
                        new ValueTuple<bool?, int, int>(
                            g.Key,
                            w.Rows.Count(y => y.Count() > 2),
                            w.Rows.Where(y => y.Count() == 2).Sum(y => y.Count())
                        )
                ),
            db =>
                InMemoryWindow<IGrouping<bool?, EdgeItem>>
                    .Over(
                        Sources.Items(db).GroupBy(x => x.Flag),
                        x => null,
                        rows => rows.OrderBy(g => g.Key),
                        g => g.Key
                    )
                    .Select(x => new ValueTuple<bool?, int, int>(
                        x.Row.Key,
                        x.Window.Rows(-1, 0).Count(y => y.Count() > 2),
                        x.Window.Rows(-1, 0)
                            .Where(y => y.Count() == 2)
                            .Sum(y => y.Count())
                    ))
        );
    }

    [Theory(
        Skip = "#111 (translator #128): a RANGE offset that is a fraction (also #123) "
            + "or beyond bigint is written as numeric over an integer key, which "
            + "PostgreSQL rejects (0A000: RANGE with offset PRECEDING/FOLLOWING is "
            + "not supported for column type integer and offset type numeric)"
    )]
    [InlineData("-0.5", "0.5")]
    [InlineData("-1.5", "0")]
    [InlineData("-79228162514264337593543950335", "0")]
    [InlineData("0", "9223372036854775808")]
    public void RangesByAnOffsetThatIsNoBigintOverAnIntegerKey(string start, string end)
    {
        decimal from = decimal.Parse(start, CultureInfo.InvariantCulture);
        decimal to = decimal.Parse(end, CultureInfo.InvariantCulture);

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
                            w.Rows.Sum(y => y.Id)
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
                                y => (decimal)y.Number!.Value,
                                (row, current) =>
                                    row - current >= from && row - current <= to
                            ),
                        ];
                        return new ValueTuple<long, int, long>(
                            x.Row.Id,
                            frame.Length,
                            frame.Sum(y => y.Id)
                        );
                    })
        );
    }

    [Theory(
        Skip = "#111 (translator #128): a RANGE offset that is a fraction (also #123) "
            + "or beyond bigint is written as numeric over a bigint key, which "
            + "PostgreSQL rejects (0A000: RANGE with offset PRECEDING/FOLLOWING is "
            + "not supported for column type bigint and offset type numeric)"
    )]
    [InlineData("-1.5", "2.5")]
    [InlineData("-0.25", "0")]
    [InlineData("-79228162514264337593543950335", "0")]
    public void RangesByAnOffsetThatIsNoBigintOverABigintKey(string start, string end)
    {
        decimal from = decimal.Parse(start, CultureInfo.InvariantCulture);
        decimal to = decimal.Parse(end, CultureInfo.InvariantCulture);

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db).Where(x => x.Big != null),
                    null,
                    rows => rows.OrderByDescending(x => x.Big),
                    PostgreSqlFrame.Range(from, to),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(y => y.Id)
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db).Where(x => x.Big != null),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Big),
                        x => x.Big
                    )
                    .Select(x =>
                    {
                        EdgeItem[] frame =
                        [
                            .. x.Window.Range(
                                y => (decimal)y.Big!.Value,
                                (row, current) =>
                                    current - row >= from && current - row <= to
                            ),
                        ];
                        return new ValueTuple<long, int, long>(
                            x.Row.Id,
                            frame.Length,
                            frame.Sum(y => y.Id)
                        );
                    })
        );
    }

    [Fact(
        Skip = "#111 (translator #128): Rows(long.MinValue, 0) writes "
            + "9223372036854775808 PRECEDING (22003: bigint out of range)"
    )]
    public void CountsTheRowsOfAFrameFromTheSmallestLong()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    x => x.Flag,
                    rows => rows.OrderBy(x => x.Id),
                    PostgreSqlFrame.Rows(long.MinValue, 0),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(y => y.Id)
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, int, long>(
                        x.Row.Id,
                        x.Window.Rows(long.MinValue, 0).Count(),
                        x.Window.Rows(long.MinValue, 0).Sum(y => y.Id)
                    ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): RangeMonths(int.MinValue, 0) writes an "
            + "interval of 2147483648 months (22015: interval field value out of "
            + "range)"
    )]
    public void CountsTheRowsOfAFrameFromTheSmallestMonthOffset()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Day != null),
                    null,
                    rows => rows.OrderBy(x => x.Day),
                    PostgreSqlFrame.RangeMonths(int.MinValue, 0),
                    (x, w) =>
                        new ValueTuple<long, int, long>(
                            x.Id,
                            w.Rows.Count(),
                            w.Rows.Sum(y => y.Id)
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
                                y => y.Day!.Value,
                                (row, current) => row <= current
                            ),
                        ];
                        return new ValueTuple<long, int, long>(
                            x.Row.Id,
                            frame.Length,
                            frame.Sum(y => y.Id)
                        );
                    })
        );
    }

    [Fact(
        Skip = "#111 (translator #128): RangeMonths(int.MinValue, 0) writes an "
            + "interval of 2147483648 months, so the statement fails even over no "
            + "rows (22015: interval field value out of range)"
    )]
    public void PlansAFrameFromTheSmallestMonthOffsetOverNoRows()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Extras(db).Where(x => x.Id < 0),
                    null,
                    rows => rows.OrderBy(x => x.Day),
                    PostgreSqlFrame.RangeMonths(int.MinValue, 0),
                    (x, w) => w.Rows.Count()
                ),
            db => []
        );
    }
}
