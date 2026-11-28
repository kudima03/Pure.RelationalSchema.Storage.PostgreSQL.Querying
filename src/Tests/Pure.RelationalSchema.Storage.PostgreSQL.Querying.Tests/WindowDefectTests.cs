using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The window of a row of <c>Values</c>: its id, its number in its partition and
/// the size of the partition.
/// </summary>
internal sealed record PartitionRank(long Id, long Number, int Size);

/// <summary>
/// Windows of <c>PostgreSqlWindows.Over</c> over stored tables in the shapes the
/// translator's issue #128 lists as defects: a window as the inner side of a join,
/// a group join or a <c>SelectMany</c>, which must be computed over every row of
/// its own source, not over the joined rows; a correlated window under
/// <c>SelectMany</c>; a window over the rows of <c>DistinctBy</c> and
/// <c>UnionBy</c>; and window functions read by other window functions, by
/// <c>FILTER</c> or beside aggregates of the windowed query. Each is compared with
/// the window computed in memory over the enumerated rows, in one statement.
/// </summary>
public sealed record WindowDefectTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public WindowDefectTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private WindowEdgeSources Sources => new WindowEdgeSources(_fixture);

    // ----------------------------------------- a window as the inner side of a join

    [Fact]
    public void JoinsStoredRowsWithAWindowOverEveryRowOfItsSource()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Join(
                    Ranked(db),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Id,
                    (parent, rank) =>
                        new ValueTuple<string, long, int>(
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Number,
                            rank.Size
                        )
                ),
            db =>
                db.Parents.AsEnumerable()
                    .Join(
                        InMemoryRanked(db),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        x => x.Id,
                        (parent, rank) =>
                            new ValueTuple<string, long, int>(
                                parent.Cells[ParentKey].Value.TextValue,
                                rank.Number,
                                rank.Size
                            )
                    )
        );
    }

    [Fact]
    public void AggregatesTheMatchesOfAWindowOverEveryRowOfItsSource()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.GroupJoin(
                    Ranked(db),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Id,
                    (parent, ranks) =>
                        new ValueTuple<string, long?, int, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            ranks.Max(x => (long?)x.Number),
                            ranks.Count(x => x.Number == 1),
                            ranks.Sum(x => x.Size)
                        )
                ),
            db =>
                db.Parents.AsEnumerable()
                    .GroupJoin(
                        InMemoryRanked(db),
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        x => x.Id,
                        (parent, ranks) =>
                            new ValueTuple<string, long?, int, int>(
                                parent.Cells[ParentId].Value.TextValue,
                                ranks.Max(x => (long?)x.Number),
                                ranks.Count(x => x.Number == 1),
                                ranks.Sum(x => x.Size)
                            )
                    )
        );
    }

    [Fact]
    public void CrossJoinsStoredRowsWithAWindowOverEveryRowOfItsSource()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<PartitionRank> ranked = Ranked(db);
                return db
                    .Parents.Where(x =>
                        long.Parse(x.Cells[ParentId].Value.TextValue) <= 2
                    )
                    .SelectMany(
                        x => ranked,
                        (parent, rank) =>
                            new ValueTuple<string, long, long, int>(
                                parent.Cells[ParentId].Value.TextValue,
                                rank.Id,
                                rank.Number,
                                rank.Size
                            )
                    );
            },
            db =>
            {
                PartitionRank[] ranked = [.. InMemoryRanked(db)];
                return db
                    .Parents.AsEnumerable()
                    .Where(x => long.Parse(x.Cells[ParentId].Value.TextValue) <= 2)
                    .SelectMany(
                        x => ranked,
                        (parent, rank) =>
                            new ValueTuple<string, long, long, int>(
                                parent.Cells[ParentId].Value.TextValue,
                                rank.Id,
                                rank.Number,
                                rank.Size
                            )
                    );
            }
        );
    }

    [Fact]
    public void JoinsStoredRowsWithAnAggregateOfEveryRowOfTheInnerTable()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "1")
                .Join(
                    db.Children.Select(x => new ValueTuple<string, string, int>(
                        x.Cells[ChildKey].Value.TextValue,
                        x.Cells[ChildId].Value.TextValue,
                        db.Children.Count()
                    )),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Item1,
                    (parent, child) =>
                        new ValueTuple<string, int>(child.Item2, child.Item3)
                )
        );
    }

    // ------------------------------------------- a correlated window under SelectMany

    [Fact]
    public void SelectsManyFromAWindowOfTheRowsOfEachRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> children = db.Children;
                return db.Parents.SelectMany(
                    parent =>
                        children
                            .Where(x =>
                                x.Cells[ChildKey].Value.TextValue
                                == parent.Cells[ParentKey].Value.TextValue
                            )
                            .Over(
                                null,
                                rows =>
                                    rows.OrderBy(x =>
                                        long.Parse(x.Cells[ChildId].Value.TextValue)
                                    ),
                                null,
                                (x, w) =>
                                    new ValueTuple<string, long, int>(
                                        x.Cells[ChildId].Value.TextValue,
                                        w.RowNumber(),
                                        w.Rows.Count()
                                    )
                            ),
                    (parent, child) =>
                        new ValueTuple<string, string, long, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Item1,
                            child.Item2,
                            child.Item3
                        )
                );
            },
            db =>
                db.Parents.AsEnumerable()
                    .SelectMany(
                        parent =>
                            NumberedChildren(db, parent.Cells[ParentKey].Value.TextValue),
                        (parent, child) =>
                            new ValueTuple<string, string, long, int>(
                                parent.Cells[ParentId].Value.TextValue,
                                child.Item1,
                                child.Item2,
                                child.Item3
                            )
                    )
        );
    }

    [Fact]
    public void SelectsManyAFilteredWindowOfTheRowsOfEachRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> children = db.Children;
                return db.Parents.SelectMany(parent =>
                    children
                        .Where(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Over(
                            null,
                            rows =>
                                rows.OrderBy(x =>
                                    long.Parse(x.Cells[ChildId].Value.TextValue)
                                ),
                            null,
                            (x, w) =>
                                new ValueTuple<string, long, int>(
                                    x.Cells[ChildId].Value.TextValue,
                                    w.RowNumber(),
                                    w.Rows.Count()
                                )
                        )
                        .Where(x => x.Item2 > 1)
                );
            },
            db =>
                db.Parents.AsEnumerable()
                    .SelectMany(parent =>
                        NumberedChildren(db, parent.Cells[ParentKey].Value.TextValue)
                            .Where(x => x.Item2 > 1)
                    )
        );
    }

    [Fact]
    public void SelectsManyAFilteredPageOfTheRowsOfEachRow()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.SelectMany(
                parent =>
                    db.Children.Where(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderByDescending(x =>
                            long.Parse(x.Cells[ChildId].Value.TextValue)
                        )
                        .Take(1)
                        .Where(x => x.Cells[ChildId].Value.TextValue != "2"),
                (parent, child) =>
                    new ValueTuple<string, string>(
                        parent.Cells[ParentId].Value.TextValue,
                        child.Cells[ChildId].Value.TextValue
                    )
            )
        );
    }

    // ------------------------------------------------ windows over DistinctBy, UnionBy

    [Fact]
    public void WindowsTheRowsOfAnOrderedDistinctBy()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources
                        .Items(db)
                        .OrderBy(x => x.Flag)
                        .ThenBy(x => x.Id)
                        .DistinctBy(x => x.Flag),
                    null,
                    rows => rows.OrderBy(x => x.Flag),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, int>(
                            x.Id,
                            w.RowNumber(),
                            w.Rows.Count()
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources
                            .Items(db)
                            .OrderBy(x => x.Flag)
                            .ThenBy(x => x.Id)
                            .DistinctBy(x => x.Flag),
                        x => null,
                        rows => rows.OrderBy(x => x.Flag),
                        x => x.Flag
                    )
                    .Select(x => new ValueTuple<long, long, int>(
                        x.Row.Id,
                        x.Window.RowNumber,
                        x.Window.Peers.Count()
                    ))
        );
    }

    [Fact]
    public void CountsTheRowsOfAnUnorderedDistinctBy()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db).DistinctBy(x => x.Flag),
                    null,
                    rows => rows.OrderByDescending(x => x.Flag),
                    null,
                    (x, w) =>
                        new ValueTuple<bool?, long, int>(
                            x.Flag,
                            w.RowNumber(),
                            w.Rows.Count()
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db).DistinctBy(x => x.Flag),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Flag),
                        x => x.Flag
                    )
                    .Select(x => new ValueTuple<bool?, long, int>(
                        x.Row.Flag,
                        x.Window.RowNumber,
                        x.Window.Peers.Count()
                    ))
        );
    }

    [Fact]
    public void WindowsTheRowsOfAUnionBy()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources
                        .Items(db)
                        .Where(x => x.Id <= 6)
                        .UnionBy(Sources.Items(db).Where(x => x.Id > 3), x => x.Flag),
                    null,
                    rows => rows.OrderBy(x => x.Flag),
                    null,
                    (x, w) =>
                        new ValueTuple<bool?, long, int>(
                            x.Flag,
                            w.RowNumber(),
                            w.Rows.Count()
                        )
                ),
            db =>
                InMemoryWindow<EdgeItem>
                    .Over(
                        Sources
                            .Items(db)
                            .Where(x => x.Id <= 6)
                            .UnionBy(Sources.Items(db).Where(x => x.Id > 3), x => x.Flag),
                        x => null,
                        rows => rows.OrderBy(x => x.Flag),
                        x => x.Flag
                    )
                    .Select(x => new ValueTuple<bool?, long, int>(
                        x.Row.Flag,
                        x.Window.RowNumber,
                        x.Window.Peers.Count()
                    ))
        );
    }

    // --------------------------------- window functions read beside window aggregates

    [Fact]
    public void AggregatesAWindowBesideItsResults()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<PartitionRank> ranked = Ranked(db);
                return ranked.Select(x => new ValueTuple<long, long, long>(
                    x.Id,
                    x.Number,
                    ranked.Max(y => y.Number)
                ));
            },
            db =>
            {
                PartitionRank[] ranked = [.. InMemoryRanked(db)];
                return ranked.Select(x => new ValueTuple<long, long, long>(
                    x.Id,
                    x.Number,
                    ranked.Max(y => y.Number)
                ));
            }
        );
    }

    [Fact]
    public void CountsTheResultsOfAWindowByAWindowFunctionBesideThem()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<PartitionRank> ranked = Ranked(db);
                return ranked.Select(x => new ValueTuple<long, int>(
                    x.Id,
                    ranked.Count(y => y.Number == 1)
                ));
            },
            db =>
            {
                PartitionRank[] ranked = [.. InMemoryRanked(db)];
                return ranked.Select(x => new ValueTuple<long, int>(
                    x.Id,
                    ranked.Count(y => y.Number == 1)
                ));
            }
        );
    }

    [Fact]
    public void AggregatesTheFrameAggregatesOfAWindowBesideThem()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> running = WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    PostgreSqlFrame.Rows(null, 0),
                    (x, w) => new ValueTuple<long, long>(x.Id, w.Rows.Sum(y => y.Id))
                );
                return running.Select(x => new ValueTuple<long, long, int>(
                    x.Item1,
                    running.Max(y => y.Item2),
                    running.Count(y => y.Item2 > 20)
                ));
            },
            db =>
            {
                ValueTuple<long, long>[] running =
                [
                    .. InMemoryWindow<EdgeItem>
                        .Over(
                            Sources.Items(db),
                            x => null,
                            rows => rows.OrderBy(x => x.Id),
                            x => x.Id
                        )
                        .Select(x => new ValueTuple<long, long>(
                            x.Row.Id,
                            x.Window.Rows(null, 0).Sum(y => y.Id)
                        )),
                ];
                return running.Select(x => new ValueTuple<long, long, int>(
                    x.Item1,
                    running.Max(y => y.Item2),
                    running.Count(y => y.Item2 > 20)
                ));
            }
        );
    }

    // ------------------------------------- window functions in arguments of others

    // A default that is a window function is read where LAG or LEAD is NULL, which
    // tells a missing row only for a value that is never NULL in a row.
    [Fact]
    public void RefusesAWindowFunctionAsTheDefaultOfLagAndLeadOfNullableValues()
    {
        _ = Enumeration.AssertRefused(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, double?, int?>(
                            x.Id,
                            w.Lag(y => y.Id, 1, w.RowNumber()),
                            w.Lead(y => (double?)y.Number, 2, w.PercentRank()),
                            w.Lag(y => y.Number, 1, w.Rows.Count())
                        )
                )
        );
    }

    // PostgreSQL nests no window function in the arguments of another.
    [Fact]
    public void RefusesAnOffsetOfLagComputedFromAWindowFunction()
    {
        _ = Enumeration.AssertRefused(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long>(
                            x.Id,
                            w.Lag(y => y.Id, (int)w.RowNumber() % 3, 0L)
                        )
                )
        );
    }

    // Over a projection of the table the aggregate stays a subquery; over the stored
    // rows themselves it does not (the test after the next).
    [Fact]
    public void ReadsAnAggregateOfATableInTheArgumentsOfWindowFunctions()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, int?, int, long>(
                            x.Id,
                            w.Lag(y => y.Number, 1, db.Values.Count()),
                            w.Ntile(
                                db.Values.Count(y =>
                                    y.Cells[Text].Value.TextValue == "plain"
                                )
                            ),
                            w.Lead(y => y.Id * db.Values.Count(), 1, 0L)
                        )
                ),
            db =>
            {
                int all = db.Values.Count();
                int flagged = db.Values.Count(y =>
                    y.Cells[Text].Value.TextValue == "plain"
                );
                return InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, int?, int, long>(
                        x.Row.Id,
                        x.Window.Lag(y => y.Number, 1, all),
                        x.Window.Ntile(flagged),
                        x.Window.Lead(y => y.Id * all, 1, 0L)
                    ));
            }
        );
    }

    [Fact]
    public void FiltersAFrameByAnAggregateOfATable()
    {
        Enumeration.AssertSameElements(
            db =>
                WindowEdgeSources.Over(
                    db,
                    Sources.Items(db),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, int>(
                            x.Id,
                            w.Rows.Count(y => y.Id < db.Values.Count() / 3)
                        )
                ),
            db =>
            {
                int third = db.Values.Count() / 3;
                return InMemoryWindow<EdgeItem>
                    .Over(
                        Sources.Items(db),
                        x => null,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, int>(
                        x.Row.Id,
                        x.Window.Peers.Count(y => y.Id < third)
                    ));
            }
        );
    }

    [Fact]
    public void ReadsAnAggregateOfTheStoredTableInTheArgumentsOfWindowFunctions()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<string, string, int, long>(
                            x.Cells[ValueId].Value.TextValue,
                            w.Lag(
                                y => y.Cells[Text].Value.TextValue,
                                1,
                                "#" + db.Values.Count()
                            ),
                            w.Ntile(
                                db.Values.Count(y =>
                                    y.Cells[Text].Value.TextValue == "plain"
                                )
                            ),
                            w.Lead(
                                y =>
                                    long.Parse(y.Cells[ValueId].Value.TextValue)
                                    * db.Values.Count(),
                                1,
                                0L
                            )
                        )
                ),
            db =>
            {
                int all = db.Values.Count();
                int plain = db.Values.Count(y =>
                    y.Cells[Text].Value.TextValue == "plain"
                );
                return InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, string, int, long>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.Lag(y => y.Cells[Text].Value.TextValue, 1, "#" + all),
                        x.Window.Ntile(plain),
                        x.Window.Lead(
                            y => long.Parse(y.Cells[ValueId].Value.TextValue) * all,
                            1,
                            0L
                        )
                    ));
            }
        );
    }

    [Fact]
    public void FiltersAFrameByAnAggregateOfTheStoredTable()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<string, int>(
                            x.Cells[ValueId].Value.TextValue,
                            w.Rows.Count(y =>
                                long.Parse(y.Cells[ValueId].Value.TextValue)
                                < db.Values.Count() / 3
                            )
                        )
                ),
            db =>
            {
                int third = db.Values.Count() / 3;
                return InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, int>(
                        x.Row.Cells[ValueId].Value.TextValue,
                        x.Window.Peers.Count(y =>
                            long.Parse(y.Cells[ValueId].Value.TextValue) < third
                        )
                    ));
            }
        );
    }

    /// <summary>
    /// The rows of <c>Values</c> numbered in each partition of their flag by their
    /// id, descending, and the size of the partition: a row a join matches is not
    /// first in its partition.
    /// </summary>
    private IQueryable<PartitionRank> Ranked(ExecutionTables db)
    {
        return WindowEdgeSources.Over(
            db,
            Sources.Items(db),
            x => x.Flag,
            rows => rows.OrderByDescending(x => x.Id),
            PostgreSqlFrame.Rows(null, null),
            (x, w) => new PartitionRank(x.Id, w.RowNumber(), w.Rows.Count())
        );
    }

    private IEnumerable<PartitionRank> InMemoryRanked(ExecutionTables db)
    {
        return InMemoryWindow<EdgeItem>
            .Over(
                Sources.Items(db),
                x => x.Flag,
                rows => rows.OrderByDescending(x => x.Id),
                x => x.Id
            )
            .Select(x => new PartitionRank(
                x.Row.Id,
                x.Window.RowNumber,
                x.Window.Rows(null, null).Count()
            ));
    }

    /// <summary>
    /// The children of the key in the order of their ids, each with its number and
    /// the count of the default frame, the children up to it.
    /// </summary>
    private IEnumerable<ValueTuple<string, long, int>> NumberedChildren(
        ExecutionTables db,
        string key
    )
    {
        return InMemoryWindow<IRow>
            .Over(
                db.Children.Where(x => x.Cells[ChildKey].Value.TextValue == key),
                x => null,
                rows => rows.OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue)),
                x => long.Parse(x.Cells[ChildId].Value.TextValue)
            )
            .Select(x => new ValueTuple<string, long, int>(
                x.Row.Cells[ChildId].Value.TextValue,
                x.Window.RowNumber,
                x.Window.Peers.Count()
            ));
    }
}
