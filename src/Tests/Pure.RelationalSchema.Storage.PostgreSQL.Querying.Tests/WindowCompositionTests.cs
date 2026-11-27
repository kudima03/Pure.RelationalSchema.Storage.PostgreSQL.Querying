#pragma warning disable xUnit1004 // Tests blocked on a fix are skipped with the reason.

using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A stored row and the window values of it, read as a record.
/// </summary>
internal sealed record RankedRow(IRow Row, long Number, double Percent);

/// <summary>
/// The window values of a row, read through an initializer after a constructor.
/// </summary>
internal sealed record RankedValue(long Id)
{
    public long Rank { get; init; }

    public long? Previous { get; init; }

    public string Label { get; init; } = "";
}

/// <summary>
/// A query of a window held in a field, composed later.
/// </summary>
internal sealed record WindowHolder(IQueryable<ValueTuple<long, long>> Ranked);

/// <summary>
/// Windows of <c>PostgreSqlWindows.Over</c> composed with the rest of a query: over
/// sources of every shape (empty, paged, distinct, a left join with missing rows, a
/// cross join of two schemas, a concatenation of one table of two schemas, a
/// grouping of a join, another window), followed by the operators that read their
/// results (filters, orders, pages, elements, aggregates, groupings, set
/// operations), inside subqueries and lambdas, captured in locals, fields and
/// methods, and building records, initializers, anonymous objects and nested tuples
/// of stored rows and window values; each compared with the window computed in
/// memory over the enumerated rows, in one statement, or refused before any runs.
/// </summary>
public sealed record WindowCompositionTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public WindowCompositionTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn EntryId => _fixture.EntryId;

    private IColumn EntryAmount => _fixture.EntryAmount;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private WindowEdgeSources Sources => new WindowEdgeSources(_fixture);

    [Fact]
    public void ReturnsNoRowsForAWindowOverNoRows()
    {
        Enumeration.AssertSameValue(
            db =>
                string.Join(
                    ";",
                    MarkerCalls.Over(
                        Sources.Items(db).Where(x => x.Id < 0),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        PostgreSqlFrame.Rows(-1, 1),
                        (x, w) =>
                            new ValueTuple<long, long, int, long?>(
                                x.Id,
                                w.RowNumber(),
                                w.Rows.Count(),
                                w.Lag(row => (long?)row.Id, 1, null)
                            )
                    )
                ),
            db => ""
        );
    }

    [Fact]
    public void CountsAndSumsAWindowOverNoRows()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db).Where(x => x.Id < 0),
                        null,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => w.RowNumber()
                    )
                    .Count(),
            db => 0
        );
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db).Where(x => x.Id < 0),
                        null,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => w.RowNumber()
                    )
                    .Sum(),
            db => 0L
        );
    }

    [Fact]
    public void WindowsOnlyTheRowsOfAPagedSource()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources.Items(db).OrderBy(x => x.Id).Skip(2).Take(6),
                    x => x.Flag,
                    rows => rows.OrderByDescending(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, int, long>(
                            x.Id,
                            w.RowNumber(),
                            w.Rows.Count(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db).OrderBy(x => x.Id).Skip(2).Take(6),
                        x => x.Flag,
                        rows => rows.OrderByDescending(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long, int, long>(
                        x.Row.Id,
                        x.Window.RowNumber,
                        x.Window.Peers.Count(),
                        x.Window.Peers.Sum(row => row.Id)
                    ))
        );
    }

    [Fact]
    public void KeepsTheOrderOfAnOrderedSource()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls.Over(
                    Sources.Items(db).OrderByDescending(x => x.Id),
                    x => x.Flag,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                ),
            db =>
                WindowEdgeSources.InSourceOrder(
                    Sources.Items(db).OrderByDescending(x => x.Id),
                    x => x.Flag,
                    rows => rows.OrderBy(x => x.Id),
                    x => x.Id,
                    (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber)
                )
        );
    }

    [Fact]
    public void KeepsTheOrderOfAPagedSource()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls.Over(
                    Sources
                        .Items(db)
                        .OrderBy(x => x.Text, StringComparer.Ordinal)
                        .ThenByDescending(x => x.Id)
                        .Take(7),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, long>(x.Id, w.RowNumber(), w.Rank())
                ),
            db =>
                WindowEdgeSources.InSourceOrder(
                    Sources
                        .Items(db)
                        .OrderBy(x => x.Text, StringComparer.Ordinal)
                        .ThenByDescending(x => x.Id)
                        .Take(7),
                    x => null,
                    rows => rows.OrderBy(x => x.Id),
                    x => x.Id,
                    (x, w) => new ValueTuple<long, long, long>(x.Id, w.RowNumber, w.Rank)
                )
        );
    }

    [Fact]
    public void WindowsTheDistinctRowsOfAProjection()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources
                        .Items(db)
                        .Select(x => new ValueTuple<bool?, string>(x.Flag, x.Text))
                        .Distinct(),
                    x => x.Item1,
                    rows => rows.OrderBy(x => x.Item2, StringComparer.Ordinal),
                    null,
                    (x, w) =>
                        new ValueTuple<bool?, string, long, int>(
                            x.Item1,
                            x.Item2,
                            w.RowNumber(),
                            w.Rows.Count()
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<bool?, string>>
                    .Over(
                        Sources
                            .Items(db)
                            .Select(x => new ValueTuple<bool?, string>(x.Flag, x.Text))
                            .Distinct(),
                        x => x.Item1,
                        rows => rows.OrderBy(x => x.Item2, StringComparer.Ordinal),
                        x => x.Item2
                    )
                    .Select(x => new ValueTuple<bool?, string, long, int>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber,
                        x.Window.Peers.Count()
                    ))
        );
    }

    [Fact]
    public void WindowsALeftJoinWhoseMissingRowsAreNull()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.GroupJoin(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, children) =>
                                new ValueTuple<IRow, IEnumerable<IRow>>(parent, children)
                        )
                        .SelectMany(
                            x => x.Item2.DefaultIfEmpty(),
                            (x, child) => new ValueTuple<IRow, IRow?>(x.Item1, child)
                        ),
                    x => x.Item1.Cells[ParentId].Value.TextValue,
                    null,
                    null,
                    (x, w) =>
                        new ValueTuple<string, IRow?, int, long>(
                            x.Item1.Cells[ParentId].Value.TextValue,
                            x.Item2,
                            w.Rows.Count(),
                            w.Rows.LongCount(row => row.Item2 != null)
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow?>>
                    .Over(
                        db.Parents.GroupJoin(
                                db.Children,
                                x => x.Cells[ParentKey].Value.TextValue,
                                x => x.Cells[ChildKey].Value.TextValue,
                                (parent, children) =>
                                    new ValueTuple<IRow, IEnumerable<IRow>>(
                                        parent,
                                        children
                                    )
                            )
                            .SelectMany(
                                x => x.Item2.DefaultIfEmpty(),
                                (x, child) => new ValueTuple<IRow, IRow?>(x.Item1, child)
                            ),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        null,
                        x => null
                    )
                    .Select(x => new ValueTuple<string, IRow?, int, long>(
                        x.Row.Item1.Cells[ParentId].Value.TextValue,
                        x.Row.Item2,
                        x.Window.Peers.Count(),
                        x.Window.Peers.LongCount(row => row.Item2 != null)
                    ))
        );
    }

    [Fact]
    public void RanksTheGroupsOfAGroupJoin()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.GroupJoin(
                        db.Children,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, children) =>
                            new ValueTuple<long, int>(
                                long.Parse(parent.Cells[ParentId].Value.TextValue),
                                children.Count()
                            )
                    ),
                    null,
                    rows => rows.OrderByDescending(x => x.Item2),
                    null,
                    (x, w) =>
                        new ValueTuple<long, int, long, long>(
                            x.Item1,
                            x.Item2,
                            w.Rank(),
                            w.DenseRank()
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<long, int>>
                    .Over(
                        db.Parents.GroupJoin(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, children) =>
                                new ValueTuple<long, int>(
                                    long.Parse(parent.Cells[ParentId].Value.TextValue),
                                    children.Count()
                                )
                        ),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Item2),
                        x => x.Item2
                    )
                    .Select(x => new ValueTuple<long, int, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.Rank,
                        x.Window.DenseRank
                    ))
        );
    }

    [Fact]
    public void WindowsACrossJoinOfTablesOfTwoSchemas()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.SelectMany(
                        x => db.Entries,
                        (parent, entry) =>
                            new ValueTuple<long, long, int?>(
                                long.Parse(parent.Cells[ParentId].Value.TextValue),
                                long.Parse(entry.Cells[EntryId].Value.TextValue),
                                string.IsNullOrEmpty(
                                    entry.Cells[EntryAmount].Value.TextValue
                                )
                                    ? null
                                    : int.Parse(entry.Cells[EntryAmount].Value.TextValue)
                            )
                    ),
                    x => x.Item1,
                    rows => rows.OrderByDescending(x => x.Item3).ThenBy(x => x.Item2),
                    PostgreSqlFrame.Rows(null, 0),
                    (x, w) =>
                        new ValueTuple<long, long, long, long?>(
                            x.Item1,
                            x.Item2,
                            w.RowNumber(),
                            w.Rows.Sum(row => (long?)row.Item3)
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<long, long, int?>>
                    .Over(
                        db.Parents.SelectMany(
                            x => db.Entries,
                            (parent, entry) =>
                                new ValueTuple<long, long, int?>(
                                    long.Parse(parent.Cells[ParentId].Value.TextValue),
                                    long.Parse(entry.Cells[EntryId].Value.TextValue),
                                    string.IsNullOrEmpty(
                                        entry.Cells[EntryAmount].Value.TextValue
                                    )
                                        ? null
                                        : int.Parse(
                                            entry.Cells[EntryAmount].Value.TextValue
                                        )
                                )
                        ),
                        x => x.Item1,
                        rows => rows.OrderByDescending(x => x.Item3).ThenBy(x => x.Item2),
                        x => (x.Item3, x.Item2)
                    )
                    .Select(x => new ValueTuple<long, long, long, long?>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber,
                        x.Window.Rows(null, 0).Sum(row => (long?)row.Item3)
                    ))
        );
    }

    [Fact]
    public void WindowsTheStoredRowsOfATableOfTheSecondSchema()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.ArchivedParents,
                    null,
                    rows =>
                        rows.OrderByDescending(x =>
                            long.Parse(x.Cells[ParentId].Value.TextValue)
                        ),
                    null,
                    (row, w) =>
                        new ValueTuple<IRow, long, string>(
                            row,
                            w.RowNumber(),
                            w.Lag(x => x.Cells[ParentKey].Value.TextValue, 1, "none")
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.ArchivedParents,
                        x => null,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<IRow, long, string>(
                        x.Row,
                        x.Window.RowNumber,
                        x.Window.Lag(
                            row => row.Cells[ParentKey].Value.TextValue,
                            1,
                            "none"
                        )
                    ))
        );
    }

    [Fact]
    public void WindowsTheRowsOfOneTableOfTwoSchemasConcatenated()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.Concat(db.ArchivedParents),
                    x => x.Cells[ParentKey].Value.TextValue,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
                    null,
                    (row, w) =>
                        new ValueTuple<string, string, long, long>(
                            row.Cells[ParentId].Value.TextValue,
                            row.Cells[ParentKey].Value.TextValue,
                            w.Rank(),
                            w.Rows.LongCount()
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Parents.Concat(db.ArchivedParents),
                        x => x.Cells[ParentKey].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, string, long, long>(
                        x.Row.Cells[ParentId].Value.TextValue,
                        x.Row.Cells[ParentKey].Value.TextValue,
                        x.Window.Rank,
                        x.Window.Peers.LongCount()
                    ))
        );
    }

    [Fact]
    public void PartitionsAJoinByAWholeStoredRow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.Join(
                        db.Children,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, child) => new ValueTuple<IRow, IRow>(parent, child)
                    ),
                    x => x.Item1,
                    rows =>
                        rows.OrderBy(x =>
                            long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                        ),
                    null,
                    (x, w) =>
                        new ValueTuple<string, string, long>(
                            x.Item1.Cells[ParentId].Value.TextValue,
                            x.Item2.Cells[ChildId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow>>
                    .Over(
                        db.Parents.Join(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, child) => new ValueTuple<IRow, IRow>(parent, child)
                        ),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                            ),
                        x => long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, string, long>(
                        x.Row.Item1.Cells[ParentId].Value.TextValue,
                        x.Row.Item2.Cells[ChildId].Value.TextValue,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void WindowsTheResultsOfAnotherWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    MarkerCalls.Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    ),
                    x => x.Item2,
                    rows => rows.OrderByDescending(x => x.Item1),
                    PostgreSqlFrame.Rows(-1, 0),
                    (x, w) =>
                        new ValueTuple<long, long, long, long>(
                            x.Item1,
                            x.Item2,
                            w.RowNumber(),
                            w.Rows.Sum(row => row.Item1)
                        )
                ),
            db =>
            {
                ValueTuple<long, long>[] inner =
                [
                    .. InMemoryWindow<EdgeItem>
                        .Over(
                            Sources.Items(db),
                            x => x.Flag,
                            rows => rows.OrderBy(x => x.Id),
                            x => x.Id
                        )
                        .Select(x => new ValueTuple<long, long>(
                            x.Row.Id,
                            x.Window.RowNumber
                        )),
                ];
                return InMemoryWindow<ValueTuple<long, long>>
                    .Over(
                        inner,
                        x => x.Item2,
                        rows => rows.OrderByDescending(x => x.Item1),
                        x => x.Item1
                    )
                    .Select(x => new ValueTuple<long, long, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber,
                        x.Window.Rows(-1, 0).Sum(row => row.Item1)
                    ));
            }
        );
    }

    [Fact]
    public void FiltersAWindowOfAWindowByBothOfItsValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        MarkerCalls.Over(
                            Sources.Items(db),
                            null,
                            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
                            null,
                            (x, w) =>
                                new ValueTuple<long, long, int?>(
                                    x.Id,
                                    w.RowNumber(),
                                    w.Lag(row => row.Number, 1, null)
                                )
                        ),
                        null,
                        rows => rows.OrderBy(x => x.Item3).ThenBy(x => x.Item1),
                        null,
                        (x, w) =>
                            new ValueTuple<long, long, long>(
                                x.Item1,
                                x.Item2,
                                w.RowNumber()
                            )
                    )
                    .Where(x => x.Item2 > x.Item3),
            db =>
            {
                ValueTuple<long, long, int?>[] inner =
                [
                    .. InMemoryWindow<EdgeItem>
                        .Over(
                            Sources.Items(db),
                            x => null,
                            rows => rows.OrderBy(x => x.Number).ThenBy(x => x.Id),
                            x => (x.Number, x.Id)
                        )
                        .Select(x => new ValueTuple<long, long, int?>(
                            x.Row.Id,
                            x.Window.RowNumber,
                            x.Window.Lag(row => row.Number, 1, null)
                        )),
                ];
                return InMemoryWindow<ValueTuple<long, long, int?>>
                    .Over(
                        inner,
                        x => null,
                        rows => rows.OrderBy(x => x.Item3).ThenBy(x => x.Item1),
                        x => (x.Item3, x.Item1)
                    )
                    .Select(x => new ValueTuple<long, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber
                    ))
                    .Where(x => x.Item2 > x.Item3);
            }
        );
    }

    [Fact]
    public void ReadsTheSingleRowOfAWindowValue()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        null,
                        rows => rows.OrderByDescending(x => x.Huge).ThenBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .Single(x => x.Item2 == 3),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Huge).ThenBy(x => x.Id),
                        x => (x.Huge, x.Id)
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .Single(x => x.Item2 == 3)
        );
    }

    [Fact]
    public void ThrowsForTheSingleRowOfAWindowValueSharedByRows()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .Single(x => x.Item2 == 1),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .Single(x => x.Item2 == 1)
        );
    }

    [Fact]
    public void ReadsTheLastAndAnElementOfOrderedWindowValues()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .OrderBy(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Last(),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .OrderBy(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .Last()
        );
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .OrderByDescending(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .ElementAt(2),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
                    .OrderByDescending(x => x.Item2)
                    .ThenBy(x => x.Item1)
                    .ElementAt(2)
        );
    }

    [Fact]
    public void ReturnsTheDefaultOfAnElementOfNoWindowValue()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        null,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .FirstOrDefault(
                        x => x.Item2 > 100,
                        new ValueTuple<long, long>(-1, -1)
                    ),
            db => new ValueTuple<long, long>(-1, -1)
        );
    }

    [Fact]
    public void TestsAndCountsWindowValues()
    {
        Enumeration.AssertSameValue(
            db => RankedById(db).Any(x => x.Item2 > 5),
            db => InMemoryRankedById(db).Any(x => x.Item2 > 5)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).All(x => x.Item2 <= 9),
            db => InMemoryRankedById(db).All(x => x.Item2 <= 9)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).Count(x => x.Item2 == 1),
            db => InMemoryRankedById(db).Count(x => x.Item2 == 1)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).LongCount(x => x.Item1 > x.Item2),
            db => InMemoryRankedById(db).LongCount(x => x.Item1 > x.Item2)
        );
    }

    [Fact]
    public void AggregatesWindowValues()
    {
        Enumeration.AssertSameValue(
            db => RankedById(db).Max(x => x.Item2),
            db => InMemoryRankedById(db).Max(x => x.Item2)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).Min(x => (x.Item2 * 10) - x.Item1),
            db => InMemoryRankedById(db).Min(x => (x.Item2 * 10) - x.Item1)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).Average(x => x.Item2),
            db => InMemoryRankedById(db).Average(x => x.Item2)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).Select(x => x.Item2).Contains(8L),
            db => InMemoryRankedById(db).Select(x => x.Item2).Contains(8L)
        );
    }

    [Fact]
    public void CountsTheRowsWhosePercentRankAndCumulativeDistributionExceedOne()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Number),
                        null,
                        (x, w) => w.PercentRank() + w.CumeDist()
                    )
                    .Count(x => x > 1.0),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Number),
                        x => x.Number
                    )
                    .Select(x => x.Window.PercentRank + x.Window.CumeDist)
                    .Count(x => x > 1.0)
        );
    }

    [Fact]
    public void GroupsWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                RankedById(db)
                    .GroupBy(x => x.Item2)
                    .Select(x => new ValueTuple<long, int, long>(
                        x.Key,
                        x.Count(),
                        x.Sum(row => row.Item1)
                    )),
            db =>
                InMemoryRankedById(db)
                    .GroupBy(x => x.Item2)
                    .Select(x => new ValueTuple<long, int, long>(
                        x.Key,
                        x.Count(),
                        x.Sum(row => row.Item1)
                    ))
        );
    }

    [Fact]
    public void ReadsTheDistinctWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Flag),
                        null,
                        (x, w) => new ValueTuple<long, long>(w.Rank(), w.DenseRank())
                    )
                    .Distinct(),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Flag),
                        x => x.Flag
                    )
                    .Select(x => new ValueTuple<long, long>(
                        x.Window.Rank,
                        x.Window.DenseRank
                    ))
                    .Distinct()
        );
    }

    [Fact]
    public void UnitesAndConcatenatesTwoWindows()
    {
        Enumeration.AssertSameElements(
            db =>
                RankedById(db)
                    .Union(
                        MarkerCalls.Over(
                            Sources.Items(db),
                            null,
                            rows => rows.OrderByDescending(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                        )
                    ),
            db =>
                InMemoryRankedById(db)
                    .Union(
                        InMemoryWindow<EdgeItem>
                            .Over(
                                Sources.Items(db),
                                x => null,
                                rows => rows.OrderByDescending(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new ValueTuple<long, long>(
                                x.Row.Id,
                                x.Window.RowNumber
                            ))
                    )
        );
        Enumeration.AssertSameElements(
            db =>
                RankedById(db)
                    .Concat(
                        MarkerCalls.Over(
                            Sources.Items(db),
                            x => x.Text,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.Rank())
                        )
                    ),
            db =>
                InMemoryRankedById(db)
                    .Concat(
                        InMemoryWindow<EdgeItem>
                            .Over(
                                Sources.Items(db),
                                x => x.Text,
                                rows => rows.OrderBy(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new ValueTuple<long, long>(
                                x.Row.Id,
                                x.Window.Rank
                            ))
                    )
        );
    }

    [Fact]
    public void IntersectsAndExceptsTwoWindows()
    {
        Enumeration.AssertSameElements(
            db =>
                RankedById(db)
                    .Intersect(
                        MarkerCalls.Over(
                            Sources.Items(db),
                            x => x.Text,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                        )
                    ),
            db =>
                InMemoryRankedById(db)
                    .Intersect(
                        InMemoryWindow<EdgeItem>
                            .Over(
                                Sources.Items(db),
                                x => x.Text,
                                rows => rows.OrderBy(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new ValueTuple<long, long>(
                                x.Row.Id,
                                x.Window.RowNumber
                            ))
                    )
        );
        Enumeration.AssertSameElements(
            db =>
                RankedById(db)
                    .Except(
                        MarkerCalls.Over(
                            Sources.Items(db),
                            x => x.Text,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                        )
                    ),
            db =>
                InMemoryRankedById(db)
                    .Except(
                        InMemoryWindow<EdgeItem>
                            .Over(
                                Sources.Items(db),
                                x => x.Text,
                                rows => rows.OrderBy(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new ValueTuple<long, long>(
                                x.Row.Id,
                                x.Window.RowNumber
                            ))
                    )
        );
    }

    [Fact]
    public void JoinsStoredRowsWithAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = RankedById(db);
                return db.Parents.Join(
                    ranked,
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Item1,
                    (parent, rank) =>
                        new ValueTuple<string, string, long>(
                            parent.Cells[ParentId].Value.TextValue,
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Item2
                        )
                );
            },
            db =>
                db.Parents.AsEnumerable()
                    .Join(
                        InMemoryRankedById(db),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        x => x.Item1,
                        (parent, rank) =>
                            new ValueTuple<string, string, long>(
                                parent.Cells[ParentId].Value.TextValue,
                                parent.Cells[ParentKey].Value.TextValue,
                                rank.Item2
                            )
                    )
        );
    }

    [Fact]
    public void CountsAWindowHeldInAFieldInASubqueryPerRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                WindowHolder holder = new WindowHolder(RankedById(db));
                return db
                    .Parents.Where(x => x.Cells[ParentNumber].Value.TextValue != "")
                    .Select(x => new ValueTuple<string, int>(
                        x.Cells[ParentId].Value.TextValue,
                        holder.Ranked.Count(rank =>
                            rank.Item2
                            <= long.Parse(x.Cells[ParentNumber].Value.TextValue)
                        )
                    ));
            },
            db =>
            {
                ValueTuple<long, long>[] ranked = [.. InMemoryRankedById(db)];
                return db
                    .Parents.AsEnumerable()
                    .Where(x => x.Cells[ParentNumber].Value.TextValue != "")
                    .Select(x => new ValueTuple<string, int>(
                        x.Cells[ParentId].Value.TextValue,
                        ranked.Count(rank =>
                            rank.Item2
                            <= long.Parse(x.Cells[ParentNumber].Value.TextValue)
                        )
                    ));
            }
        );
    }

    [Fact]
    public void ReadsTheMaximumOfAWindowReturnedByAMethodInASubquery()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Where(x => x.Cells[ParentNumber].Value.TextValue != "")
                    .Select(x => new ValueTuple<string, long>(
                        x.Cells[ParentId].Value.TextValue,
                        RankedById(db)
                            .Where(rank =>
                                rank.Item1
                                >= long.Parse(x.Cells[ParentNumber].Value.TextValue)
                            )
                            .Max(rank => rank.Item2)
                    )),
            db =>
            {
                ValueTuple<long, long>[] ranked = [.. InMemoryRankedById(db)];
                return db
                    .Parents.AsEnumerable()
                    .Where(x => x.Cells[ParentNumber].Value.TextValue != "")
                    .Select(x => new ValueTuple<string, long>(
                        x.Cells[ParentId].Value.TextValue,
                        ranked
                            .Where(rank =>
                                rank.Item1
                                >= long.Parse(x.Cells[ParentNumber].Value.TextValue)
                            )
                            .Max(rank => rank.Item2)
                    ));
            }
        );
    }

    [Fact]
    public void WindowsTheRowsOfACorrelatedSubqueryWrittenInTheTree()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Select(parent => new ValueTuple<string, long>(
                    parent.Cells[ParentId].Value.TextValue,
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Over(
                            null,
                            rows =>
                                rows.OrderBy(x =>
                                    long.Parse(x.Cells[ChildId].Value.TextValue)
                                ),
                            null,
                            (child, w) =>
                                w.RowNumber()
                                * long.Parse(child.Cells[ChildId].Value.TextValue)
                        )
                        .Sum()
                )),
            db =>
                db.Parents.AsEnumerable()
                    .Select(parent => new ValueTuple<string, long>(
                        parent.Cells[ParentId].Value.TextValue,
                        db.Children.AsEnumerable()
                            .Where(child =>
                                child.Cells[ChildKey].Value.TextValue
                                == parent.Cells[ParentKey].Value.TextValue
                            )
                            .OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                            .Select(
                                (child, index) =>
                                    (index + 1L)
                                    * long.Parse(child.Cells[ChildId].Value.TextValue)
                            )
                            .Sum()
                    ))
        );
    }

    [Fact]
    public void WindowsAFrameOfALocalInACorrelatedSubqueryWrittenInTheTree()
    {
        PostgreSqlFrame frame = PostgreSqlFrame.Rows(-1, 0);

        Enumeration.AssertSameElements(
            db =>
                db.Parents.Select(parent => new ValueTuple<string, int>(
                    parent.Cells[ParentId].Value.TextValue,
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Over(
                            null,
                            rows =>
                                rows.OrderBy(x =>
                                    long.Parse(x.Cells[ChildId].Value.TextValue)
                                ),
                            frame,
                            (child, w) => w.Rows.Count()
                        )
                        .Sum()
                )),
            db =>
                db.Parents.AsEnumerable()
                    .Select(parent => new ValueTuple<string, int>(
                        parent.Cells[ParentId].Value.TextValue,
                        Math.Max(
                            0,
                            (
                                2
                                * db.Children.AsEnumerable()
                                    .Count(child =>
                                        child.Cells[ChildKey].Value.TextValue
                                        == parent.Cells[ParentKey].Value.TextValue
                                    )
                            ) - 1
                        )
                    ))
        );
    }

    [Fact]
    public void ReadsARecordOfAStoredRowAndItsWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Children,
                        x => x.Cells[ChildKey].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            ),
                        null,
                        (row, w) => new RankedRow(row, w.RowNumber(), w.PercentRank())
                    )
                    .AsEnumerable()
                    .Select(x => new ValueTuple<IRow, long, double>(
                        x.Row,
                        x.Number,
                        x.Percent
                    ))
                    .AsQueryable(),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Children,
                        x => x.Cells[ChildKey].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ChildId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<IRow, long, double>(
                        x.Row,
                        x.Window.RowNumber,
                        x.Window.PercentRank
                    ))
        );
    }

    [Fact]
    public void ReadsTheStoredRowOfARecordOfAWindowAfterAFilter()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Children,
                        x => x.Cells[ChildKey].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            ),
                        null,
                        (row, w) => new RankedRow(row, w.RowNumber(), w.CumeDist())
                    )
                    .Where(x => x.Number == 1)
                    .Select(x => x.Row),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Children,
                        x => x.Cells[ChildKey].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ChildId].Value.TextValue)
                    )
                    .Where(x => x.Window.RowNumber == 1)
                    .Select(x => x.Row)
        );
    }

    [Fact]
    public void ReadsAnInitializerOfWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources.Items(db),
                    x => x.Flag,
                    rows => rows.OrderBy(x => x.Big).ThenBy(x => x.Id),
                    null,
                    (x, w) =>
                        new RankedValue(x.Id)
                        {
                            Rank = w.Rank(),
                            Previous = w.Lag(row => (long?)row.Id, 1, null),
                            Label = x.Text + "#" + w.RowNumber(),
                        }
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Big).ThenBy(x => x.Id),
                        x => (x.Big, x.Id)
                    )
                    .Select(x => new RankedValue(x.Row.Id)
                    {
                        Rank = x.Window.Rank,
                        Previous = x.Window.Lag(row => (long?)row.Id, 1, null),
                        Label = x.Row.Text + "#" + x.Window.RowNumber,
                    })
        );
    }

    [Fact]
    public void FiltersByAnInitializerAssignmentReplacingTheConstructorArgument()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderByDescending(x => x.Id),
                        null,
                        (x, w) =>
                            new RankedValue(x.Id)
                            {
                                Id = w.RowNumber(),
                                Rank = x.Id,
                                Label = x.Text,
                            }
                    )
                    .Where(x => x.Id <= 2)
                    .Select(x => new ValueTuple<long, long, string>(
                        x.Id,
                        x.Rank,
                        x.Label
                    )),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderByDescending(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new RankedValue(x.Row.Id)
                    {
                        Id = x.Window.RowNumber,
                        Rank = x.Row.Id,
                        Label = x.Row.Text,
                    })
                    .Where(x => x.Id <= 2)
                    .Select(x => new ValueTuple<long, long, string>(
                        x.Id,
                        x.Rank,
                        x.Label
                    ))
        );
    }

    [Fact]
    public void ReadsAnInitializerAssignmentReplacingTheConstructorArgument()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) => new RankedValue(x.Id) { Id = (w.RowNumber() * 100) + x.Id }
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new RankedValue(x.Row.Id)
                    {
                        Id = (x.Window.RowNumber * 100) + x.Row.Id,
                    })
        );
    }

    [Fact]
    public void ThrowsForTheMinimumOfNoWindowValuesAndReadsTheDefaultOfNone()
    {
        Enumeration.AssertSameValue(
            db => RankedById(db).Where(x => x.Item2 > 100).MinBy(x => x.Item1),
            db => InMemoryRankedById(db).Where(x => x.Item2 > 100).MinBy(x => x.Item1)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).OrderBy(x => x.Item1).ElementAtOrDefault(100),
            db => InMemoryRankedById(db).OrderBy(x => x.Item1).ElementAtOrDefault(100)
        );
        Enumeration.AssertSameValue(
            db => RankedById(db).Where(x => x.Item2 > 100).Select(x => x.Item2).Single(),
            db =>
                InMemoryRankedById(db)
                    .Where(x => x.Item2 > 100)
                    .Select(x => x.Item2)
                    .Single()
        );
    }

    [Fact]
    public void ReadsAnAnonymousObjectOfAStoredRowAndWindowValuesThroughAProjection()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents,
                        null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        PostgreSqlFrame.Rows(0, 1),
                        (row, w) =>
                            new
                            {
                                Row = row,
                                Next = w.Lead(
                                    x => x.Cells[ParentKey].Value.TextValue,
                                    1,
                                    "end"
                                ),
                                Count = w.Rows.Count(),
                            }
                    )
                    .Select(x => new ValueTuple<IRow, string, int>(
                        x.Row,
                        x.Next,
                        x.Count
                    )),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Parents,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<IRow, string, int>(
                        x.Row,
                        x.Window.Lead(
                            row => row.Cells[ParentKey].Value.TextValue,
                            1,
                            "end"
                        ),
                        x.Window.Rows(0, 1).Count()
                    ))
        );
    }

    [Fact]
    public void ReadsAnAnonymousObjectOfAStoredRowAndWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents,
                        null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        null,
                        (row, w) => new { Row = row, Number = w.RowNumber() }
                    )
                    .AsEnumerable()
                    .Select(x => new ValueTuple<IRow, long>(x.Row, x.Number))
                    .AsQueryable(),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Parents,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<IRow, long>(x.Row, x.Window.RowNumber))
        );
    }

    [Fact]
    public void ReadsNestedTuplesOfTheSourceElementAndWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) =>
                            new ValueTuple<EdgeItem, ValueTuple<long, long>, long?>(
                                x,
                                new ValueTuple<long, long>(w.RowNumber(), w.DenseRank()),
                                w.Lead(row => row.Big, 2, null)
                            )
                    )
                    .AsEnumerable()
                    .Select(x => new ValueTuple<string, long, long, long?>(
                        x.Item1.ToString(),
                        x.Item2.Item1,
                        x.Item2.Item2,
                        x.Item3
                    ))
                    .AsQueryable(),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<string, long, long, long?>(
                        x.Row.ToString(),
                        x.Window.RowNumber,
                        x.Window.DenseRank,
                        x.Window.Lead(row => row.Big, 2, null)
                    ))
        );
    }

    [Fact]
    public void ReadsTheSourceElementItselfAsTheResultOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Extras(db),
                        null,
                        rows => rows.OrderBy(x => x.Clock),
                        null,
                        (x, w) => x
                    )
                    .AsEnumerable()
                    .Select(x => x.ToString())
                    .AsQueryable(),
            db => Sources.Extras(db).AsEnumerable().Select(x => x.ToString())
        );
    }

    [Fact]
    public void EnumeratesAWindowAsynchronously()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                    .ToAsyncEnumerable()
                    .ToListAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult()
                    .AsQueryable(),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
        );
    }

    [Fact]
    public void CastsTheResultsOfAWindowCreatedWithoutItsElementType()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Values.Provider.CreateQuery(
                        MarkerCalls.OverCall<EdgeItem, ValueTuple<long, long>>(
                            Sources.Items(db).Expression,
                            x => x.Text,
                            rows => rows.OrderByDescending(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                        )
                    )
                    .Cast<ValueTuple<long, long>>(),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Text,
                        rows => rows.OrderByDescending(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber))
        );
    }

    [Fact]
    public void ExecutesTheCountOfAWindowThroughTheProvider()
    {
        Enumeration.AssertSameValue(
            db =>
                db.Values.Provider.Execute<int>(
                    System.Linq.Expressions.Expression.Call(
                        typeof(Queryable),
                        nameof(Queryable.Count),
                        [typeof(ValueTuple<long, long>)],
                        RankedById(db).Expression
                    )
                ),
            db => InMemoryRankedById(db).Count()
        );
    }

    [Fact]
    public void RefusesARangeOfAnIntervalOverANumber()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.OrderBy(x => x.Id),
                PostgreSqlFrame.Range(TimeSpan.FromSeconds(-1), TimeSpan.Zero),
                (x, w) => w.Rows.Count()
            )
        );
    }

    [Fact]
    public void RefusesARangeOfANumberOverADate()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Extras(db),
                null,
                rows => rows.OrderBy(x => x.Day),
                PostgreSqlFrame.Range(-1m, 0m),
                (x, w) => w.Rows.Count()
            )
        );
    }

    [Fact]
    public void RefusesARangeOfAnOffsetWithoutAnOrder()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                x => x.Flag,
                null,
                PostgreSqlFrame.Range(-1m, 1m),
                (x, w) => w.Rows.Count()
            )
        );
    }

    [Fact]
    public void RefusesARangeOfAnOffsetOverText()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.OrderBy(x => x.Text, StringComparer.Ordinal),
                PostgreSqlFrame.Range(null, 1m),
                (x, w) => w.Rows.Count()
            )
        );
    }

    [Fact]
    public void RefusesAnOrderOfAWindowThatIsNotASortOfItsRows()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.Where(x => x.Id > 1).OrderBy(x => x.Id),
                null,
                (x, w) => w.RowNumber()
            )
        );
    }

    [Fact]
    public void RefusesAnOrderOfAWindowByACultureComparer()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows =>
                    rows.OrderBy(x => x.Text, StringComparer.InvariantCultureIgnoreCase),
                null,
                (x, w) => w.RowNumber()
            )
        );
    }

    [Fact]
    public void RefusesTheFirstValueOfAPossiblyEmptyFrameReadAsAValue()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.OrderBy(x => x.Id),
                PostgreSqlFrame.Rows(1, 2),
                (x, w) => w.FirstValue(row => row.Id)
            )
        );
    }

    [Fact]
    public void RefusesAFrameAggregateReadingTheCurrentRow()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.OrderBy(x => x.Id),
                null,
                (x, w) => w.Rows.Count(row => row.Id < x.Id)
            )
        );
    }

    [Fact]
    public void RefusesAWindowReadInsideTheLambdaOfAFrameAggregate()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                Sources.Items(db),
                null,
                rows => rows.OrderBy(x => x.Id),
                null,
                (x, w) => w.Rows.Sum(row => w.RowNumber())
            )
        );
    }

    /// <summary>
    /// The rows of <c>Values</c> numbered by their id in the partitions of their flag.
    /// </summary>
    private IQueryable<ValueTuple<long, long>> RankedById(ExecutionTables db)
    {
        return MarkerCalls.Over(
            Sources.Items(db),
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
        );
    }

    private IEnumerable<ValueTuple<long, long>> InMemoryRankedById(ExecutionTables db)
    {
        return InMemoryWindow<EdgeItem>
            .Over(
                Sources.Items(db),
                x => x.Flag,
                rows => rows.OrderBy(x => x.Id),
                x => x.Id
            )
            .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber));
    }
}
