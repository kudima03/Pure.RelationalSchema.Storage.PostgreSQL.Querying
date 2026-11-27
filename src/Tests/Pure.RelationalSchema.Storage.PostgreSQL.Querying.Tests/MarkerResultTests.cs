using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of semantics translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A value of the <c>Values</c> table read as its types, ranked by a window.
/// </summary>
internal sealed record RankedValue(long Id, bool? Flag, long? Big, string Text);

/// <summary>
/// A rank of a value built by an initializer.
/// </summary>
internal sealed record RankedHolder
{
    public long Id { get; set; }

    public long Rank { get; set; }

    public string? Text { get; set; }
}

/// <summary>
/// The operators of LINQ applied directly to a PostgreSQL marker — the element
/// operators with the exception or default LINQ to Objects gives where there is no
/// element, the aggregates, and the rows a marker returns read again through their
/// cells, their count and their keys — return in one statement what the same
/// operators return over the window or the levels computed in memory.
/// </summary>
public sealed record MarkerResultTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public MarkerResultTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Big => _fixture.Big;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsTheFirstResultOfAnOrderedWindow()
    {
        AssertRanks(ranks =>
            ranks.OrderBy(x => x.Item2).ThenByDescending(x => x.Item1).First()
        );
    }

    [Fact]
    public void ThrowsForTheFirstResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.First());
    }

    [Fact]
    public void ThrowsForTheFirstResultOfAWindowMatchingNoPredicate()
    {
        AssertRanks(ranks => ranks.First(x => x.Item2 > 100));
    }

    [Fact]
    public void ReadsTheDefaultForTheFirstResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.FirstOrDefault());
    }

    [Fact]
    public void ReadsTheGivenDefaultForTheFirstResultMatchingNoPredicate()
    {
        AssertRanks(ranks =>
            ranks.FirstOrDefault(x => x.Item2 > 100, new ValueTuple<long, long>(-1, -2))
        );
    }

    [Fact(
        Skip = "#111 (translator #141): the translator refuses FirstOrDefault(source, defaultValue) "
            + "(The method 'Queryable.FirstOrDefault' is not supported); "
            + "TranslatedQuery.Default reads a given default only as the third "
            + "argument, so it would then return default(T)."
    )]
    public void ReadsTheGivenDefaultForTheFirstResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.FirstOrDefault(new ValueTuple<long, long>(-1, -2)));
    }

    [Fact(
        Skip = "#111 (translator #141): the translator refuses LastOrDefault(source, defaultValue) "
            + "(The method 'Queryable.LastOrDefault' is not supported); "
            + "TranslatedQuery.Default reads a given default only as the third "
            + "argument, so it would then return default(T)."
    )]
    public void ReadsTheGivenDefaultForTheLastResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks =>
            ranks.OrderBy(x => x.Item1).LastOrDefault(new ValueTuple<long, long>(-1, -2))
        );
    }

    [Fact(
        Skip = "#111 (translator #141): the translator refuses SingleOrDefault(source, "
            + "defaultValue) (The method 'Queryable.SingleOrDefault' is not "
            + "supported); TranslatedQuery.Default reads a given default only as "
            + "the third argument, so it would then return default(T)."
    )]
    public void ReadsTheGivenDefaultForTheSingleResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.SingleOrDefault(new ValueTuple<long, long>(-1, -2)));
    }

    [Fact]
    public void ReadsTheSingleResultOfAWindow()
    {
        AssertRanks(ranks => ranks.Single(x => x.Item1 == 3));
    }

    [Fact]
    public void ThrowsForTheSingleResultOfAWindowOfManyResults()
    {
        AssertRanks(ranks => ranks.Single(x => x.Item2 == 1));
    }

    [Fact]
    public void ReadsTheGivenDefaultForTheSingleResultMatchingNoPredicate()
    {
        AssertRanks(ranks =>
            ranks.SingleOrDefault(x => x.Item1 == 5, new ValueTuple<long, long>(-1, -2))
        );
    }

    [Fact]
    public void ThrowsForTheSingleOrDefaultResultOfAWindowOfManyResults()
    {
        AssertRanks(ranks =>
            ranks.SingleOrDefault(x => x.Item2 == 2, new ValueTuple<long, long>(-1, -2))
        );
    }

    [Fact]
    public void ReadsTheLastResultOfAnOrderedWindow()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item2).ThenBy(x => x.Item1).Last());
    }

    [Fact]
    public void ThrowsForTheLastResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.OrderBy(x => x.Item1).Last());
    }

    [Fact]
    public void ReadsTheResultOfAWindowAtAnIndex()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item1).ElementAt(3));
    }

    [Fact]
    public void ThrowsForTheResultOfAWindowPastTheLastIndex()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item1).ElementAt(100));
    }

    [Fact]
    public void ReadsTheDefaultForTheResultOfAWindowPastTheLastIndex()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item1).ElementAtOrDefault(100));
    }

    [Fact]
    public void ReadsTheResultsOfAWindowWithTheLeastAndGreatestKeys()
    {
        AssertRanks(ranks => ranks.MinBy(x => (x.Item2 * 1000) - x.Item1));
        AssertRanks(ranks => ranks.MaxBy(x => (x.Item2 * 1000) + x.Item1));
    }

    [Fact]
    public void ThrowsForTheResultWithTheLeastKeyOfAnEmptyWindowOfValues()
    {
        AssertNoRanks(ranks => ranks.MinBy(x => x.Item1));
    }

    [Fact]
    public void ReadsNullForTheResultWithTheGreatestKeyOfAnEmptyWindowOfObjects()
    {
        Enumeration.AssertSameValue(
            db => Holders(db, "none").MaxBy(x => x.Id),
            db => InMemoryHolders(db, "none").AsQueryable().MaxBy(x => x.Id)
        );
    }

    [Fact]
    public void ReadsObjectsOfAWindowBuiltByAnInitializer()
    {
        Enumeration.AssertSameElements(
            db => Holders(db, "plain"),
            db => InMemoryHolders(db, "plain")
        );
    }

    [Fact]
    public void CountsTheResultsOfAWindow()
    {
        AssertRanks(ranks => ranks.Count());
        AssertRanks(ranks => ranks.LongCount(x => x.Item2 > 1));
        AssertNoRanks(ranks => ranks.Count());
    }

    [Fact]
    public void SumsAndAveragesTheResultsOfAWindow()
    {
        AssertRanks(ranks => ranks.Sum(x => x.Item2 * x.Item1));
        AssertRanks(ranks => ranks.Average(x => x.Item2));
        AssertNoRanks(ranks => ranks.Sum(x => x.Item2));
    }

    [Fact]
    public void ThrowsForTheAverageOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.Average(x => x.Item2));
    }

    [Fact]
    public void ThrowsForTheGreatestResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.Max(x => x.Item2));
    }

    [Fact]
    public void ReadsNullForTheGreatestNullableResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.Max(x => (long?)x.Item2));
        AssertNoRanks(ranks => ranks.Average(x => (long?)x.Item2));
    }

    [Fact]
    public void TestsTheResultsOfAWindow()
    {
        AssertRanks(ranks => ranks.Any(x => x.Item2 > 3));
        AssertRanks(ranks => ranks.All(x => x.Item2 >= 1));
        AssertRanks(ranks => ranks.Select(x => x.Item2).Contains(6L));
        AssertNoRanks(ranks => ranks.Any());
        AssertNoRanks(ranks => ranks.All(x => x.Item2 > 100));
    }

    [Fact]
    public void ReadsAnElementAndAggregatesOfTheRowsOfARecursion()
    {
        Enumeration.AssertSameValue(
            db =>
                Walk(db)
                    .Single(x => x.Cells[ParentId].Value.TextValue == "4")
                    .Cells.Count,
            db =>
                InMemoryWalk(db)
                    .Single(x => x.Cells[ParentId].Value.TextValue == "4")
                    .Cells.Count
        );
        Enumeration.AssertSameValue(
            db => Walk(db).Count(),
            db => InMemoryWalk(db).Count()
        );
        Enumeration.AssertSameValue(
            db => Walk(db).FirstOrDefault(x => x.Cells[ParentId].Value.TextValue == "9"),
            db =>
                InMemoryWalk(db)
                    .FirstOrDefault(x => x.Cells[ParentId].Value.TextValue == "9")
        );
        Enumeration.AssertSameValue(
            db => Walk(db).Max(x => x.Cells[ParentKey].Value.TextValue.Length),
            db => InMemoryWalk(db).Max(x => x.Cells[ParentKey].Value.TextValue.Length)
        );
    }

    [Fact]
    public void ReadsTheCellsOfTheRowsOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(db.Values, null, null, null, (x, w) => x)
                    .Select(x => new ValueTuple<string, string, int, int>(
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Text].Value.TextValue,
                        x.Cells.Count,
                        x.Cells.Keys.Count()
                    )),
            db =>
                db.Values.Select(x => new ValueTuple<string, string, int, int>(
                    x.Cells[ValueId].Value.TextValue,
                    x.Cells[Text].Value.TextValue,
                    x.Cells.Count,
                    x.Cells.Keys.Count()
                ))
        );
    }

    [Fact]
    public void ReadsTheRowsOfAWindowBare()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Children,
                    x => x.Cells[ChildKey].Value.TextValue,
                    null,
                    null,
                    (x, w) => x
                ),
            db => db.Children
        );
    }

    [Fact]
    public void FiltersTheRowsOfAWindowByTheirTypedCellsAndCount()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Values,
                        null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        null,
                        (x, w) => new ValueTuple<IRow, long>(x, w.RowNumber())
                    )
                    .Where(x =>
                        x.Item1.Cells.Count == 10
                        && (
                            string.IsNullOrEmpty(x.Item1.Cells[Big].Value.TextValue)
                                ? null
                                : long.Parse(x.Item1.Cells[Big].Value.TextValue)
                        ) < 1
                    )
                    .Select(x => new ValueTuple<string, long>(
                        x.Item1.Cells[Text].Value.TextValue,
                        x.Item2
                    )),
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
                    .Where(x =>
                        x.Row.Cells.Count == 10
                        && (
                            string.IsNullOrEmpty(x.Row.Cells[Big].Value.TextValue)
                                ? null
                                : long.Parse(x.Row.Cells[Big].Value.TextValue)
                        ) < 1
                    )
                    .Select(x => new ValueTuple<string, long>(
                        x.Row.Cells[Text].Value.TextValue,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void ReadsTheFirstRowOfAWindowMatchingACell()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(db.Values, null, null, null, (x, w) => x)
                    .OrderByDescending(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                    .First(x => x.Cells[Text].Value.TextValue == "plain"),
            db =>
                db.Values.OrderByDescending(x =>
                        long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .First(x => x.Cells[Text].Value.TextValue == "plain")
        );
    }

    [Fact]
    public void ReadsTheCellsOfTheRowsOfARecursion()
    {
        Enumeration.AssertSameElements(
            db =>
                Walk(db)
                    .Select(x => new ValueTuple<string, string, int>(
                        x.Cells[ParentId].Value.TextValue,
                        x.Cells[ParentKey].Value.TextValue,
                        x.Cells.Count
                    )),
            db =>
                InMemoryWalk(db)
                    .Select(x => new ValueTuple<string, string, int>(
                        x.Cells[ParentId].Value.TextValue,
                        x.Cells[ParentKey].Value.TextValue,
                        x.Cells.Count
                    ))
        );
    }

    [Fact]
    public void WindowsTheOptionalRowsOfALeftJoin()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    LeftJoined(db),
                    x => x.Item1.Cells[ParentId].Value.TextValue,
                    rows =>
                        rows.OrderBy(x =>
                            x.Item2 == null
                                ? ""
                                : x.Item2.Cells[ChildLabel].Value.TextValue
                        ),
                    null,
                    (x, w) => new ValueTuple<IRow?, long>(x.Item2, w.RowNumber())
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow?>>
                    .Over(
                        LeftJoined(db),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderBy(
                                x =>
                                    x.Item2 == null
                                        ? ""
                                        : x.Item2.Cells[ChildLabel].Value.TextValue,
                                StringComparer.Ordinal
                            ),
                        x =>
                            x.Item2 == null
                                ? ""
                                : x.Item2.Cells[ChildLabel].Value.TextValue
                    )
                    .Select(x => new ValueTuple<IRow?, long>(
                        x.Row.Item2,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void WindowsTheResultsOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Ranks(db),
                    null,
                    rows => rows.OrderByDescending(x => x.Item2).ThenBy(x => x.Item1),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, long>(x.Item1, x.Item2, w.RowNumber())
                ),
            db =>
                InMemoryWindow<ValueTuple<long, long>>
                    .Over(
                        InMemoryRanks(db),
                        x => null,
                        rows => rows.OrderByDescending(x => x.Item2).ThenBy(x => x.Item1),
                        x => x
                    )
                    .Select(x => new ValueTuple<long, long, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void SeedsARecursionWithTheResultsOfAWindow()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level
                .Where(x => x.Depth < 3)
                .Select(x => new WalkNode(x.Id, x.Depth + 1, x.Path));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        MarkerCalls.Over(
                            Items(db),
                            x => x.Flag,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new WalkNode(x.Id, w.RowNumber(), x.Text)
                        ),
                        step
                    )
                    .Select(x => new ValueTuple<long, long, string>(
                        x.Id,
                        x.Depth,
                        x.Path
                    )),
            db =>
                Levels(
                        InMemoryWindow<RankedValue>
                            .Over(
                                Items(db),
                                x => x.Flag,
                                rows => rows.OrderBy(x => x.Id),
                                x => x.Id
                            )
                            .Select(x => new WalkNode(
                                x.Row.Id,
                                x.Window.RowNumber,
                                x.Row.Text
                            ))
                            .AsQueryable(),
                        step.Compile()
                    )
                    .Select(x => new ValueTuple<long, long, string>(
                        x.Id,
                        x.Depth,
                        x.Path
                    ))
        );
    }

    [Fact]
    public void WindowsTheRowsOfARecursion()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level
                .Where(x => x.Depth < 2)
                .Select(x => new WalkNode(x.Id * 2, x.Depth + 1, ""));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    MarkerCalls.WithRecursive(Seed(db), step),
                    x => x.Depth,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) => new ValueTuple<long, long, long>(x.Id, x.Depth, w.Rank())
                ),
            db =>
                InMemoryWindow<WalkNode>
                    .Over(
                        Levels(Seed(db), step.Compile()),
                        x => x.Depth,
                        rows => rows.OrderBy(x => x.Id),
                        x => x.Id
                    )
                    .Select(x => new ValueTuple<long, long, long>(
                        x.Row.Id,
                        x.Row.Depth,
                        x.Window.Rank
                    ))
        );
    }

    [Fact]
    public void WindowsTheGroupsOfAGroupingDirectly()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Items(db).GroupBy(x => x.Text),
                    null,
                    rows => rows.OrderBy(x => x.Count()),
                    null,
                    (x, w) =>
                        new ValueTuple<string, int, long>(x.Key, x.Count(), w.Rank())
                ),
            db =>
                InMemoryWindow<IGrouping<string, RankedValue>>
                    .Over(
                        Items(db).GroupBy(x => x.Text),
                        x => null,
                        rows => rows.OrderBy(x => x.Count()),
                        x => x.Count()
                    )
                    .Select(x => new ValueTuple<string, int, long>(
                        x.Row.Key,
                        x.Row.Count(),
                        x.Window.Rank
                    ))
        );
    }

    [Fact]
    public void KeepsTheOrderOfTheSourceOfAWindow()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls.Over(
                    Items(db).OrderByDescending(x => x.Id),
                    null,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                ),
            db =>
                Items(db)
                    .AsEnumerable()
                    .OrderBy(x => x.Id)
                    .Select((x, index) => new ValueTuple<long, long>(x.Id, index + 1))
                    .OrderByDescending(x => x.Item1)
        );
    }

    [Fact(
        Skip = "#111: row.Cells.Count in w.Rows.Sum(row => ...) is refused "
            + "(Specified method is not supported, from EntityExpression.Columns): "
            + "EntityExpression binds the lambdas of Queryable operators and "
            + "markers only, not those of Enumerable operators over the rows of a "
            + "frame."
    )]
    public void SumsTheCountOfTheCellsOfTheRowsOfAFrame()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents,
                    null,
                    null,
                    null,
                    (x, w) =>
                        new ValueTuple<string, int>(
                            x.Cells[ParentId].Value.TextValue,
                            w.Rows.Sum(row => row.Cells.Count)
                        )
                ),
            db =>
                db.Parents.AsEnumerable()
                    .Select(x => new ValueTuple<string, int>(
                        x.Cells[ParentId].Value.TextValue,
                        db.Parents.Sum(row => row.Cells.Count)
                    ))
        );
    }

    [Fact]
    public void ReadsTheSameResultsWhenAWindowRunsTwice()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        IQueryable<ValueTuple<long, long>> ranks = Ranks(db);

        ValueTuple<long, long>[] first = [.. ranks.OrderBy(x => x.Item1)];
        ValueTuple<long, long>[] second = [.. ranks.OrderBy(x => x.Item1)];
        long count = ranks.LongCount();

        Assert.Equal(first, second);
        Assert.Equal(13, first.Length);
        Assert.Equal(13, count);
        Assert.Equal(3, connection.Statements.Count);
        Assert.Equal(connection.Statements[0], connection.Statements[1]);
    }

    [Fact]
    public void ThrowsForTheResultOfAWindowAtANegativeIndex()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item1).ElementAt(-1));
    }

    [Fact]
    public void ReadsTheDefaultForTheResultOfAWindowAtANegativeIndex()
    {
        AssertRanks(ranks => ranks.OrderBy(x => x.Item1).ElementAtOrDefault(-1));
    }

    [Fact]
    public void TakesNoResultsOfAWindowForANegativeCount()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        ValueTuple<long, long>[] taken = [.. Ranks(db).OrderBy(x => x.Item1).Take(-1)];

        Assert.Empty(taken);
        _ = Assert.Single(connection.Statements);
    }

    [Fact]
    public void SkipsNoResultsOfAWindowForANegativeCount()
    {
        Enumeration.AssertSameOrder(
            db => Ranks(db).OrderBy(x => x.Item1).Skip(-1),
            db => InMemoryRanks(db).OrderBy(x => x.Item1).Skip(-1)
        );
    }

    [Fact]
    public void ReadsZeroForTheNullableSumOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.Sum(x => (long?)x.Item2));
    }

    [Fact]
    public void ReadsNullForTheLeastNullableResultOfAnEmptyWindow()
    {
        AssertNoRanks(ranks => ranks.Min(x => (long?)x.Item2));
    }

    [Fact]
    public void ReadsTheSingleOrDefaultResultOfAWindowWithoutAPredicate()
    {
        AssertNoRanks(ranks => ranks.SingleOrDefault());
        AssertRanks(ranks => ranks.Where(x => x.Item1 == 7).SingleOrDefault());
        AssertRanks(ranks => ranks.SingleOrDefault());
    }

    [Fact]
    public void CountsTheLevelsOfARecursionWithinAMaximumDepth()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Items(db).Select(x => x.Id).Where(x => x < 5),
                        level => level.Where(x => x < 8).Select(x => x + 2),
                        5
                    )
                    .LongCount(x => x > 3),
            db =>
                Levels(
                        Items(db).Select(x => x.Id).Where(x => x < 5),
                        level => level.Where(x => x < 8).Select(x => x + 2)
                    )
                    .LongCount(x => x > 3)
        );
    }

    /// <summary>
    /// The ranks of the values, by id in their partition of flags.
    /// </summary>
    private IQueryable<ValueTuple<long, long>> Ranks(ExecutionTables db)
    {
        return MarkerCalls.Over(
            Items(db),
            x => x.Flag,
            rows => rows.OrderBy(x => x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
        );
    }

    private IEnumerable<ValueTuple<long, long>> InMemoryRanks(ExecutionTables db)
    {
        return InMemoryWindow<RankedValue>
            .Over(Items(db), x => x.Flag, rows => rows.OrderBy(x => x.Id), x => x.Id)
            .Select(x => new ValueTuple<long, long>(x.Row.Id, x.Window.RowNumber));
    }

    private IQueryable<RankedHolder> Holders(ExecutionTables db, string text)
    {
        return MarkerCalls.Over(
            Items(db).Where(x => x.Text == text),
            null,
            rows => rows.OrderByDescending(x => x.Id),
            null,
            (x, w) =>
                new RankedHolder
                {
                    Id = x.Id,
                    Rank = w.RowNumber(),
                    Text = x.Text,
                }
        );
    }

    private IEnumerable<RankedHolder> InMemoryHolders(ExecutionTables db, string text)
    {
        return InMemoryWindow<RankedValue>
            .Over(
                Items(db).Where(x => x.Text == text),
                x => null,
                rows => rows.OrderByDescending(x => x.Id),
                x => x.Id
            )
            .Select(x => new RankedHolder
            {
                Id = x.Row.Id,
                Rank = x.Window.RowNumber,
                Text = x.Row.Text,
            });
    }

    /// <summary>
    /// The operator over the ranks, translated, returns what it returns over the
    /// ranks computed in memory.
    /// </summary>
    private void AssertRanks<T>(Func<IQueryable<ValueTuple<long, long>>, T> operation)
    {
        Enumeration.AssertSameValue(
            db => operation(Ranks(db)),
            db => operation(InMemoryRanks(db).AsQueryable())
        );
    }

    /// <summary>
    /// The operator over the ranks of no rows, translated, returns what it returns
    /// over no ranks in memory.
    /// </summary>
    private void AssertNoRanks<T>(Func<IQueryable<ValueTuple<long, long>>, T> operation)
    {
        Enumeration.AssertSameValue(
            db =>
                operation(
                    MarkerCalls.Over(
                        Items(db).Where(x => x.Id > 1000),
                        x => x.Flag,
                        rows => rows.OrderBy(x => x.Id),
                        null,
                        (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                    )
                ),
            db => operation(Array.Empty<ValueTuple<long, long>>().AsQueryable())
        );
    }

    /// <summary>
    /// The parents with the id 2 and the one after it, walked by the id.
    /// </summary>
    private IQueryable<IRow> Walk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentId].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue == "2" ? "4" : "0",
                    (parent, before) => parent
                )
        );
    }

    private IQueryable<IRow> InMemoryWalk(ExecutionTables db)
    {
        return db.Parents.Where(x =>
            x.Cells[ParentId].Value.TextValue == "2"
            || x.Cells[ParentId].Value.TextValue == "4"
        );
    }

    private IQueryable<ValueTuple<IRow, IRow?>> LeftJoined(ExecutionTables db)
    {
        return db
            .Parents.GroupJoin(
                db.Children,
                x => x.Cells[ParentKey].Value.TextValue,
                x => x.Cells[ChildKey].Value.TextValue,
                (parent, children) => new { Parent = parent, Children = children }
            )
            .SelectMany(
                x => x.Children.DefaultIfEmpty(),
                (x, child) => new ValueTuple<IRow, IRow?>(x.Parent, child)
            );
    }

    private IQueryable<WalkNode> Seed(ExecutionTables db)
    {
        return db
            .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
            .Select(x => new WalkNode(
                long.Parse(x.Cells[ValueId].Value.TextValue),
                0,
                x.Cells[Text].Value.TextValue
            ));
    }

    private static List<T> Levels<T>(
        IQueryable<T> seed,
        Func<IQueryable<T>, IQueryable<T>> step
    )
    {
        List<T> rows = [];
        T[] level = [.. seed];
        while (level.Length > 0)
        {
            rows.AddRange(level);
            level = [.. step(level.AsQueryable())];
        }
        return rows;
    }

    private IQueryable<RankedValue> Items(ExecutionTables db)
    {
        return db.Values.Select(x => new RankedValue(
            long.Parse(x.Cells[ValueId].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                ? null
                : bool.Parse(x.Cells[Flag].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                ? null
                : long.Parse(x.Cells[Big].Value.TextValue),
            x.Cells[Text].Value.TextValue
        ));
    }
}
