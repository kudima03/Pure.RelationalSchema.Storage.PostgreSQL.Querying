using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The results of <c>PostgreSqlWindows.Over</c> read by the rest of a query: stored
/// rows a window returns, read through their cells, ordered, joined and combined
/// with rows of the same table of another schema or refused beside rows of another
/// table; the text of cells of every column type read in another row by
/// <c>LAG</c>, <c>LEAD</c> and <c>FIRST_VALUE</c>; windows of nullable comparisons,
/// of a recursion, of a default of no rows, of an in-memory sequence joined with
/// stored rows and of a subquery of the selector; each compared with the window
/// computed in memory over the enumerated rows, in one statement, or refused before
/// any runs.
/// </summary>
public sealed record WindowResultTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public WindowResultTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ExtraId => _fixture.ExtraId;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private WindowEdgeSources Sources => new WindowEdgeSources(_fixture);

    [Fact]
    public void ReadsTheCellsOfTheStoredRowsOfAWindowAfterIt()
    {
        Enumeration.AssertSameElements(
            db =>
                NumberedParents(db)
                    .Where(x => x.Item1.Cells[ParentKey].Value.TextValue != "b")
                    .Select(x => new ValueTuple<int, string, long>(
                        x.Item1.Cells.Count,
                        x.Item1.Cells[ParentKey].Value.TextValue,
                        x.Item2
                    )),
            db =>
                InMemoryNumberedParents(db)
                    .Where(x => x.Item1.Cells[ParentKey].Value.TextValue != "b")
                    .Select(x => new ValueTuple<int, string, long>(
                        x.Item1.Cells.Count,
                        x.Item1.Cells[ParentKey].Value.TextValue,
                        x.Item2
                    ))
        );
    }

    [Fact]
    public void OrdersTheStoredRowsOfAWindowByTheirCells()
    {
        Enumeration.AssertSameOrder(
            db =>
                NumberedParents(db)
                    .OrderByDescending(x => x.Item2)
                    .ThenBy(x => long.Parse(x.Item1.Cells[ParentId].Value.TextValue))
                    .Select(x => x.Item1),
            db =>
                InMemoryNumberedParents(db)
                    .OrderByDescending(x => x.Item2)
                    .ThenBy(x => long.Parse(x.Item1.Cells[ParentId].Value.TextValue))
                    .Select(x => x.Item1)
        );
    }

    [Fact]
    public void ReadsTheFirstStoredRowOfAWindowAndTheRowOfTheLargestValue()
    {
        Enumeration.AssertSameValue(
            db =>
                NumberedParents(db)
                    .OrderBy(x => x.Item2)
                    .ThenByDescending(x =>
                        long.Parse(x.Item1.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => x.Item1)
                    .First(),
            db =>
                InMemoryNumberedParents(db)
                    .OrderBy(x => x.Item2)
                    .ThenByDescending(x =>
                        long.Parse(x.Item1.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => x.Item1)
                    .First()
        );
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        Sources.Items(db),
                        null,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber() * 7 % 5)
                    )
                    .MaxBy(x => (x.Item2 * 100) + x.Item1),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long>(
                        x.Row.Id,
                        x.Window.RowNumber * 7 % 5
                    ))
                    .MaxBy(x => (x.Item2 * 100) + x.Item1)
        );
    }

    [Fact]
    public void JoinsTheStoredRowsOfAWindowWithAnotherTable()
    {
        Enumeration.AssertSameElements(
            db =>
                NumberedParents(db)
                    .Join(
                        db.Children,
                        x => x.Item1.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, child) =>
                            new ValueTuple<IRow, IRow, long>(
                                parent.Item1,
                                child,
                                parent.Item2
                            )
                    ),
            db =>
                InMemoryNumberedParents(db)
                    .Join(
                        db.Children,
                        x => x.Item1.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, child) =>
                            new ValueTuple<IRow, IRow, long>(
                                parent.Item1,
                                child,
                                parent.Item2
                            )
                    )
        );
    }

    [Fact]
    public void UnitesTheStoredRowsOfAWindowWithRowsOfTheTableInAnotherSchema()
    {
        Enumeration.AssertSameElements(
            db =>
                NumberedParents(db)
                    .Where(x => x.Item2 > 1)
                    .Select(x => x.Item1)
                    .Union(db.ArchivedParents),
            db =>
                InMemoryNumberedParents(db)
                    .Where(x => x.Item2 > 1)
                    .Select(x => x.Item1)
                    .Concat(db.ArchivedParents)
        );
    }

    [Fact]
    public void ConcatenatesWindowsOfOneTableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(
            db =>
                NumberedParents(db)
                    .Concat(
                        MarkerCalls.Over(
                            db.ArchivedParents,
                            null,
                            rows =>
                                rows.OrderBy(
                                    x => x.Cells[ParentKey].Value.TextValue,
                                    StringComparer.Ordinal
                                ),
                            null,
                            (row, w) => new ValueTuple<IRow, long>(row, w.RowNumber())
                        )
                    ),
            db =>
                InMemoryNumberedParents(db)
                    .Concat(
                        InMemoryWindow<IRow>
                            .Over(
                                db.ArchivedParents,
                                x => null,
                                rows =>
                                    rows.OrderBy(
                                        x => x.Cells[ParentKey].Value.TextValue,
                                        StringComparer.Ordinal
                                    ),
                                x => x.Cells[ParentKey].Value.TextValue
                            )
                            .Select(x => new ValueTuple<IRow, long>(
                                x.Row,
                                x.Window.RowNumber
                            ))
                    )
        );
    }

    [Fact]
    public void RefusesAUnionOfTheStoredRowsOfAWindowWithRowsOfAnotherTable()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls
                .Over(db.Parents, null, null, null, (row, w) => row)
                .Union(db.Children)
        );
    }

    [Fact]
    public void RefusesAConcatenationOfWindowsOfRowsOfDifferentTables()
    {
        _ = Enumeration.AssertRefused(db =>
            NumberedParents(db)
                .Concat(
                    MarkerCalls.Over(
                        db.Children,
                        null,
                        null,
                        null,
                        (row, w) => new ValueTuple<IRow, long>(row, w.RowNumber())
                    )
                )
        );
    }

    [Fact]
    public void RefusesALagOfAStoredRow()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                db.Parents,
                null,
                rows => rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
                null,
                (row, w) =>
                    new ValueTuple<IRow, IRow?>(row, w.Lag(x => (IRow?)x, 1, null))
            )
        );
    }

    [Fact]
    public void ReadsTheTextOfCellsOfEveryTypeInOtherRowsOfAWindow()
    {
        ExecutionFixture f = _fixture;

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Extras,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ExtraId].Value.TextValue)),
                    PostgreSqlFrame.Rows(1, null),
                    (row, w) =>
                        new ValueTuple<string, string, string, string, string, string?>(
                            row.Cells[ExtraId].Value.TextValue,
                            w.Lag(x => x.Cells[f.Day].Value.TextValue, 1, "-"),
                            w.Lead(x => x.Cells[f.Clock].Value.TextValue, 1, "-"),
                            w.Lag(x => x.Cells[f.Measure].Value.TextValue, 1, "-"),
                            w.Lead(x => x.Cells[f.Ratio].Value.TextValue, 1, "-"),
                            w.FirstValue(x => x.Cells[f.Digest].Value.TextValue)
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Extras,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ExtraId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ExtraId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string?
                    >(
                        x.Row.Cells[ExtraId].Value.TextValue,
                        x.Window.Lag(row => row.Cells[f.Day].Value.TextValue, 1, "-"),
                        x.Window.Lead(row => row.Cells[f.Clock].Value.TextValue, 1, "-"),
                        x.Window.Lag(row => row.Cells[f.Measure].Value.TextValue, 1, "-"),
                        x.Window.Lead(row => row.Cells[f.Ratio].Value.TextValue, 1, "-"),
                        x.Window.Rows(1, null)
                            .Select(row => row.Cells[f.Digest].Value.TextValue)
                            .FirstOrDefault()
                    ))
        );
    }

    [Fact]
    public void ReadsTheTextOfCellsOfTheValuesInOtherRowsOfAWindow()
    {
        ExecutionFixture f = _fixture;

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x => x.Cells[f.Flag].Value.TextValue,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (row, w) =>
                        new ValueTuple<string, string, string, string, string, string>(
                            row.Cells[ValueId].Value.TextValue,
                            w.Lag(x => x.Cells[f.Flag].Value.TextValue, 1, "-"),
                            w.Lead(x => x.Cells[f.Small].Value.TextValue, 1, "-"),
                            w.Lag(x => x.Cells[f.Unsigned].Value.TextValue, 1, "-"),
                            w.Lead(x => x.Cells[f.Huge].Value.TextValue, 1, "-"),
                            w.FirstValue(x => x.Cells[f.Moment].Value.TextValue)
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => x.Cells[f.Flag].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string
                    >(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.Lag(row => row.Cells[f.Flag].Value.TextValue, 1, "-"),
                        x.Window.Lead(row => row.Cells[f.Small].Value.TextValue, 1, "-"),
                        x.Window.Lag(
                            row => row.Cells[f.Unsigned].Value.TextValue,
                            1,
                            "-"
                        ),
                        x.Window.Lead(row => row.Cells[f.Huge].Value.TextValue, 1, "-"),
                        x.Window.Peers.First().Cells[f.Moment].Value.TextValue
                    ))
        );
    }

    [Fact]
    public void PartitionsByANullableComparisonAsDotNetGroupsIt()
    {
        AssertItems(
            x => x.Number > 0,
            rows => rows.OrderBy(x => x.Id),
            x => x.Id,
            (x, w) =>
                new ValueTuple<long, long, int>(x.Id, w.RowNumber(), w.Rows.Count()),
            (x, w) => new ValueTuple<long, long, int>(x.Id, w.RowNumber, w.Peers.Count())
        );
    }

    [Fact]
    public void RanksByANullableComparisonAsDotNetOrdersIt()
    {
        AssertItems(
            null,
            rows => rows.OrderByDescending(x => x.Big < 0).ThenBy(x => x.Code == null),
            x => (x.Big < 0, x.Code == null),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank(), w.DenseRank()),
            (x, w) => new ValueTuple<long, long, long>(x.Id, w.Rank, w.DenseRank)
        );
    }

    [Fact]
    public void RanksByACoalescedValueAndATextLength()
    {
        AssertItems(
            x => x.Text.Length > 4,
            rows => rows.OrderBy(x => x.Number ?? 5).ThenByDescending(x => x.Small ?? 3),
            x => (x.Number ?? 5, x.Small ?? 3),
            (x, w) => new ValueTuple<long, long, double>(x.Id, w.Rank(), w.CumeDist()),
            (x, w) => new ValueTuple<long, long, double>(x.Id, w.Rank, w.CumeDist)
        );
    }

    [Fact]
    public void WindowsTheLevelsOfARecursion()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    MarkerCalls.WithRecursive(
                        db.Values.Where(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue) < 5
                            )
                            .Select(x => new WalkNode(
                                long.Parse(x.Cells[ValueId].Value.TextValue),
                                0,
                                x.Cells[Text].Value.TextValue
                            )),
                        level =>
                            level
                                .Where(x => x.Depth < 3)
                                .Select(x => new WalkNode(
                                    x.Id * 3 % 7,
                                    x.Depth + 1,
                                    x.Path
                                ))
                    ),
                    x => x.Depth,
                    rows => rows.OrderByDescending(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, long, long>(
                            x.Id,
                            x.Depth,
                            w.Rank(),
                            w.Rows.Sum(row => row.Id)
                        )
                ),
            db =>
            {
                List<WalkNode> nodes =
                [
                    .. db
                        .Values.AsEnumerable()
                        .Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                        .Select(x => new WalkNode(
                            long.Parse(x.Cells[ValueId].Value.TextValue),
                            0,
                            x.Cells[Text].Value.TextValue
                        )),
                ];
                List<WalkNode> level = [.. nodes];
                while (level.Count > 0)
                {
                    level =
                    [
                        .. level
                            .Where(x => x.Depth < 3)
                            .Select(x => new WalkNode(x.Id * 3 % 7, x.Depth + 1, x.Path)),
                    ];
                    nodes.AddRange(level);
                }
                return InMemoryWindow<WalkNode>
                    .Over(
                        nodes,
                        x => x.Depth,
                        rows => rows.OrderByDescending(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long, long, long>(
                        x.Row.Id,
                        x.Row.Depth,
                        x.Window.Rank,
                        x.Window.Peers.Sum(row => row.Id)
                    ));
            }
        );
    }

    [Fact]
    public void WindowsTheDefaultOfAProjectionOfNoRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources
                        .Items(db)
                        .Where(x => x.Id < 0)
                        .Select(x => x.Id)
                        .DefaultIfEmpty(-7),
                    null,
                    null,
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, int>(x, w.RowNumber(), w.Rows.Count())
                ),
            db => [new ValueTuple<long, long, int>(-7, 1, 1)]
        );
    }

    [Fact]
    public void WindowsTheDefaultOfNoStoredRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values.Where(x => x.Cells[Text].Value.TextValue == "none")
                        .DefaultIfEmpty(),
                    null,
                    null,
                    null,
                    (row, w) => new ValueTuple<IRow?, long>(row, w.RowNumber())
                ),
            db => new[] { new ValueTuple<IRow?, long>(null, 1) }.AsQueryable()
        );
    }

    [Fact]
    public void WindowsAnInMemorySequenceJoinedWithStoredRows()
    {
        (long Id, string Label)[] labels =
        [
            (1, "a"),
            (2, "b"),
            (3, "a"),
            (91, "a"),
            (5, "b"),
        ];

        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    labels
                        .AsQueryable()
                        .Join(
                            db.Values,
                            x => x.Id,
                            x => long.Parse(x.Cells[ValueId].Value.TextValue),
                            (label, row) => new ValueTuple<string, IRow>(label.Label, row)
                        ),
                    x => x.Item1,
                    rows =>
                        rows.OrderByDescending(x =>
                            long.Parse(x.Item2.Cells[ValueId].Value.TextValue)
                        ),
                    null,
                    (x, w) =>
                        new ValueTuple<string, IRow, long, string>(
                            x.Item1,
                            x.Item2,
                            w.RowNumber(),
                            w.Lag(
                                row => row.Item2.Cells[Text].Value.TextValue,
                                1,
                                "first"
                            )
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<string, IRow>>
                    .Over(
                        labels.Join(
                            db.Values,
                            x => x.Id,
                            x => long.Parse(x.Cells[ValueId].Value.TextValue),
                            (label, row) => new ValueTuple<string, IRow>(label.Label, row)
                        ),
                        x => x.Item1,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Item2.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Item2.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, IRow, long, string>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber,
                        x.Window.Lag(
                            row => row.Item2.Cells[Text].Value.TextValue,
                            1,
                            "first"
                        )
                    ))
        );
    }

    [Fact]
    public void ReadsACorrelatedSubqueryOfTheCurrentRowBesideWindowValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
                    null,
                    (row, w) =>
                        new ValueTuple<string, long, int>(
                            row.Cells[ParentId].Value.TextValue,
                            w.RowNumber(),
                            db.Children.Count(child =>
                                child.Cells[ChildKey].Value.TextValue
                                == row.Cells[ParentKey].Value.TextValue
                            )
                        )
                ),
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
                    .Select(x => new ValueTuple<string, long, int>(
                        x.Row.Cells[ParentId].Value.TextValue,
                        x.Window.RowNumber,
                        db.Children.Count(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == x.Row.Cells[ParentKey].Value.TextValue
                        )
                    ))
        );
    }

    [Fact]
    public void ReadsACapturedWindowInTheSelectorOfAnotherWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = MarkerCalls.Over(
                    Sources.Items(db),
                    x => x.Flag,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                );
                return MarkerCalls.Over(
                    db.Parents,
                    null,
                    rows =>
                        rows.OrderByDescending(x =>
                            long.Parse(x.Cells[ParentId].Value.TextValue)
                        ),
                    null,
                    (row, w) =>
                        new ValueTuple<string, long, int>(
                            row.Cells[ParentId].Value.TextValue,
                            w.RowNumber(),
                            ranked.Count(x => x.Item2 == 1)
                        )
                );
            },
            db =>
            {
                int firsts = InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Count(x => x.Window.RowNumber == 1);
                return InMemoryWindow<IRow>
                    .Over(
                        db.Parents,
                        x => null,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, long, int>(
                        x.Row.Cells[ParentId].Value.TextValue,
                        x.Window.RowNumber,
                        firsts
                    ));
            }
        );
    }

    [Fact]
    public void FiltersStoredRowsByAWindowWrittenInThePredicate()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Where(parent =>
                    db.Children.Over(
                            x => x.Cells[ChildKey].Value.TextValue,
                            rows =>
                                rows.OrderByDescending(x =>
                                    long.Parse(x.Cells[ChildId].Value.TextValue)
                                ),
                            PostgreSqlFrame.Rows(null, null),
                            (child, w) =>
                                new ValueTuple<string, long, int>(
                                    child.Cells[ChildKey].Value.TextValue,
                                    w.RowNumber(),
                                    w.Rows.Count()
                                )
                        )
                        .Any(x =>
                            x.Item1 == parent.Cells[ParentKey].Value.TextValue
                            && x.Item3 > 1
                        )
                ),
            db =>
                db.Parents.AsEnumerable()
                    .Where(parent =>
                        db.Children.Count(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        ) > 1
                    )
        );
    }

    [Fact]
    public void PagesTheOrderedResultsOfAWindowAndWindowsThemAgain()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls
                    .Over(
                        MarkerCalls
                            .Over(
                                Sources.Items(db),
                                x => x.Flag,
                                rows => rows.OrderByDescending(x => x.Id),
                                null,
                                (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                            )
                            .OrderBy(x => x.Item2)
                            .ThenBy(x => x.Item1)
                            .Skip(2)
                            .Take(6),
                        null,
                        rows => rows.OrderBy(x => x.Item1),
                        null,
                        (x, w) =>
                            new ValueTuple<long, long, long>(
                                x.Item1,
                                x.Item2,
                                w.RowNumber()
                            )
                    )
                    .OrderBy(x => x.Item3),
            db =>
                InMemoryWindow<ValueTuple<long, long>>
                    .Over(
                        InMemoryWindow<EdgeItem>
                            .Over(
                                Sources.Items(db),
                                x => x.Flag,
                                rows => rows.OrderByDescending(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new ValueTuple<long, long>(
                                x.Row.Id,
                                x.Window.RowNumber
                            ))
                            .OrderBy(x => x.Item2)
                            .ThenBy(x => x.Item1)
                            .Skip(2)
                            .Take(6),
                        x => null,
                        rows => rows.OrderBy(x => x.Item1),
                        x => x.Item1
                    )
                    .Select(x => new ValueTuple<long, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber
                    ))
                    .OrderBy(x => x.Item3)
        );
    }

    [Fact]
    public void WindowsTheGroupsOfAGroupingOrderedByTheirCount()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Sources.Items(db).GroupBy(x => x.Flag),
                    null,
                    rows => rows.OrderByDescending(x => x.Count()),
                    null,
                    (x, w) =>
                        new ValueTuple<bool?, int, long, long>(
                            x.Key,
                            x.Count(),
                            x.Sum(row => row.Id),
                            w.DenseRank()
                        )
                ),
            db =>
                InMemoryWindow<IGrouping<bool?, EdgeItem>>
                    .Over(
                        Sources.Items(db).AsEnumerable().GroupBy(x => x.Flag),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Count()),
                        x => x.Count()
                    )
                    .Select(x => new ValueTuple<bool?, int, long, long>(
                        x.Row.Key,
                        x.Row.Count(),
                        x.Row.Sum(row => row.Id),
                        x.Window.DenseRank
                    ))
        );
    }

    [Fact]
    public void WindowsTheRowsOfAJoinPartitionedByTheParentAndReadsTheChildRow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.Join(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, child) => new { Parent = parent, Child = child }
                        ),
                        x => x.Parent.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Child.Cells[ChildId].Value.TextValue)
                            ),
                        null,
                        (x, w) => new { x.Child, Number = w.RowNumber() }
                    )
                    .Where(x => x.Number == 1)
                    .Select(x => x.Child),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow>>
                    .Over(
                        db.Parents.AsEnumerable()
                            .Join(
                                db.Children,
                                x => x.Cells[ParentKey].Value.TextValue,
                                x => x.Cells[ChildKey].Value.TextValue,
                                (parent, child) =>
                                    new ValueTuple<IRow, IRow>(parent, child)
                            ),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                            ),
                        x => long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                    )
                    .Where(x => x.Window.RowNumber == 1)
                    .Select(x => x.Row.Item2)
        );
    }

    [Fact]
    public void CountsTheChildrenOfEachParentThroughAWindowOfALeftJoin()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.GroupJoin(
                                db.Children,
                                x => x.Cells[ParentKey].Value.TextValue,
                                x => x.Cells[ChildKey].Value.TextValue,
                                (parent, children) =>
                                    new { Parent = parent, Children = children }
                            )
                            .SelectMany(
                                x => x.Children.DefaultIfEmpty(),
                                (x, child) => new { x.Parent, Child = child }
                            ),
                        x => x.Parent.Cells[ParentId].Value.TextValue,
                        null,
                        null,
                        (x, w) =>
                            new
                            {
                                x.Parent,
                                Count = w.Rows.Count(row => row.Child != null),
                            }
                    )
                    .Distinct()
                    .Select(x => new ValueTuple<IRow, int>(x.Parent, x.Count)),
            db =>
                db.Parents.AsEnumerable()
                    .Select(parent => new ValueTuple<IRow, int>(
                        parent,
                        db.Children.Count(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                    ))
        );
    }

    [Fact]
    public void WindowsAJoinOfOneTableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.Join(
                        db.ArchivedParents,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ParentKey].Value.TextValue,
                        (stored, archived) => new ValueTuple<IRow, IRow>(stored, archived)
                    ),
                    x => x.Item2.Cells[ParentId].Value.TextValue,
                    rows =>
                        rows.OrderByDescending(
                            x => x.Item1.Cells[ParentId].Value.TextValue,
                            StringComparer.Ordinal
                        ),
                    null,
                    (x, w) =>
                        new ValueTuple<IRow, IRow, string, long>(
                            x.Item1,
                            x.Item2,
                            x.Item2.Cells[ParentNumber].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow>>
                    .Over(
                        db.Parents.AsEnumerable()
                            .Join(
                                db.ArchivedParents,
                                x => x.Cells[ParentKey].Value.TextValue,
                                x => x.Cells[ParentKey].Value.TextValue,
                                (stored, archived) =>
                                    new ValueTuple<IRow, IRow>(stored, archived)
                            ),
                        x => x.Item2.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(
                                x => x.Item1.Cells[ParentId].Value.TextValue,
                                StringComparer.Ordinal
                            ),
                        x => x.Item1.Cells[ParentId].Value.TextValue
                    )
                    .Select(x => new ValueTuple<IRow, IRow, string, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Row.Item2.Cells[ParentNumber].Value.TextValue,
                        x.Window.RowNumber
                    ))
        );
    }

    /// <summary>
    /// The rows of <c>Parents</c> of the <c>Store</c> schema numbered in the partitions
    /// of their key by their id.
    /// </summary>
    private IQueryable<ValueTuple<IRow, long>> NumberedParents(ExecutionTables db)
    {
        return MarkerCalls.Over(
            db.Parents,
            x => x.Cells[ParentKey].Value.TextValue,
            rows => rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
            null,
            (row, w) => new ValueTuple<IRow, long>(row, w.RowNumber())
        );
    }

    private IEnumerable<ValueTuple<IRow, long>> InMemoryNumberedParents(
        ExecutionTables db
    )
    {
        return InMemoryWindow<IRow>
            .Over(
                db.Parents,
                x => x.Cells[ParentKey].Value.TextValue,
                rows => rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
                x => long.Parse(x.Cells[ParentId].Value.TextValue)
            )
            .Select(x => new ValueTuple<IRow, long>(x.Row, x.Window.RowNumber));
    }

    private void AssertItems<TResult>(
        System.Linq.Expressions.Expression<Func<EdgeItem, object?>>? partitionBy,
        System.Linq.Expressions.Expression<
            Func<IEnumerable<EdgeItem>, IOrderedEnumerable<EdgeItem>>
        >? orderBy,
        Func<EdgeItem, object?> peerKey,
        System.Linq.Expressions.Expression<
            Func<EdgeItem, PostgreSqlWindow<EdgeItem>, TResult>
        > selector,
        Func<EdgeItem, InMemoryWindow<EdgeItem>, TResult> expected
    )
    {
        Func<EdgeItem, object?> partition = partitionBy?.Compile() ?? (_ => null);
        Func<IEnumerable<EdgeItem>, IOrderedEnumerable<EdgeItem>>? order =
            orderBy?.Compile();
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(Sources.Items(db), partitionBy, orderBy, null, selector),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(Sources.Items(db), partition, order, peerKey)
                    .Select(x => expected(x.Row, x.Window))
        );
    }
}
