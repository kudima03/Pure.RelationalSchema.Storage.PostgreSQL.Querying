using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A label of an in-memory sequence joined to a row, read into a record.
/// </summary>
internal sealed record LabelledRow(long Id, string? Label, string Text);

/// <summary>
/// The shapes the elements of the PostgreSQL markers and of in-memory sequences are
/// read as — rows of the tables a tuple of a join holds, nested tuples, objects
/// whose initializer replaces a constructor argument, optional rows of an empty
/// result, records of in-memory values — and the queries composed over them:
/// grouped, joined, run again with other captured values, widened or refused by
/// the translator.
/// </summary>
public sealed record MarkerShapeTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] Labels =
    [
        (1, "one"),
        (2, null),
        (91, ""),
        (500, "unmatched"),
    ];

    private readonly ExecutionFixture _fixture;

    public MarkerShapeTests(ExecutionFixture fixture)
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

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsTheRowOfAJoinedTupleSelectedByAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(Joined(db), null, null, null, (x, w) => x.Item2)
                    .Select(x => new ValueTuple<string, int, string>(
                        x.Cells[ChildId].Value.TextValue,
                        x.Cells.Count,
                        x.Cells[ChildLabel].Value.TextValue
                    )),
            db =>
                Joined(db)
                    .Select(x => x.Item2)
                    .Select(x => new ValueTuple<string, int, string>(
                        x.Cells[ChildId].Value.TextValue,
                        x.Cells.Count,
                        x.Cells[ChildLabel].Value.TextValue
                    ))
        );
    }

    [Fact]
    public void ReadsBothRowsOfAJoinedTupleBesideTheirWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Joined(db),
                    x => x.Item1.Cells[ParentId].Value.TextValue,
                    rows =>
                        rows.OrderBy(x =>
                            long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                        ),
                    null,
                    (x, w) =>
                        new ValueTuple<IRow, IRow, long>(x.Item1, x.Item2, w.RowNumber())
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow>>
                    .Over(
                        Joined(db),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                            ),
                        x => long.Parse(x.Item2.Cells[ChildId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<IRow, IRow, long>(
                        x.Row.Item1,
                        x.Row.Item2,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void ReadsNestedTuplesOfAWindow()
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
                        (x, w) =>
                            new ValueTuple<IRow, ValueTuple<long, string>>(
                                x,
                                new ValueTuple<long, string>(
                                    w.RowNumber(),
                                    x.Cells[ParentKey].Value.TextValue
                                )
                            )
                    )
                    .Select(x => new ValueTuple<long, string, int>(
                        x.Item2.Item1,
                        x.Item2.Item2 + x.Item1.Cells[ParentId].Value.TextValue,
                        x.Item1.Cells.Count
                    )),
            db =>
                db.Parents.AsEnumerable()
                    .OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select(
                        (x, index) =>
                            new ValueTuple<long, string, int>(
                                index + 1,
                                x.Cells[ParentKey].Value.TextValue
                                    + x.Cells[ParentId].Value.TextValue,
                                x.Cells.Count
                            )
                    )
        );
    }

    [Fact]
    public void ReadsARecordOfAWindowWhoseInitializerReplacesAnArgument()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents,
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new WalkNode(
                            long.Parse(x.Cells[ParentId].Value.TextValue),
                            w.RowNumber(),
                            ""
                        )
                        {
                            Path = x.Cells[ParentKey].Value.TextValue,
                        }
                ),
            db =>
                db.Parents.AsEnumerable()
                    .OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select(
                        (x, index) =>
                            new WalkNode(
                                long.Parse(x.Cells[ParentId].Value.TextValue),
                                index + 1,
                                ""
                            )
                            {
                                Path = x.Cells[ParentKey].Value.TextValue,
                            }
                    )
        );
    }

    [Fact]
    public void SeedsARecursionWithARecordWhoseInitializerReplacesAnArgument()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        db.Parents.Select(x => new WalkNode(
                            long.Parse(x.Cells[ParentId].Value.TextValue),
                            0,
                            ""
                        )
                        {
                            Path = x.Cells[ParentKey].Value.TextValue,
                        }),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new WalkNode(
                                    x.Id + 1,
                                    x.Depth + 1,
                                    x.Path + "+"
                                ))
                    )
                    .Select(x => new ValueTuple<long, long, string>(
                        x.Id,
                        x.Depth,
                        x.Path
                    )),
            db =>
                db.Parents.AsEnumerable()
                    .SelectMany(x =>
                        Enumerable
                            .Range(0, 3)
                            .Select(depth => new ValueTuple<long, long, string>(
                                long.Parse(x.Cells[ParentId].Value.TextValue) + depth,
                                depth,
                                x.Cells[ParentKey].Value.TextValue
                                    + new string('+', depth)
                            ))
                    )
        );
    }

    [Fact]
    public void ReadsTheDefaultRowOfAnEmptyWindowOfRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.Where(x =>
                            x.Cells[ParentId].Value.TextValue == "none"
                        ),
                        null,
                        null,
                        null,
                        (x, w) => x
                    )
                    .DefaultIfEmpty(),
            db =>
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "none")
                    .DefaultIfEmpty()
        );
    }

    [Fact]
    public void ReadsTheDefaultValueOfAnEmptyWindowOfValues()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.Where(x =>
                            x.Cells[ParentId].Value.TextValue == "none"
                        ),
                        null,
                        null,
                        null,
                        (x, w) => w.RowNumber()
                    )
                    .DefaultIfEmpty(),
            db => [0]
        );
    }

    [Fact]
    public void GroupsTheResultsOfAWindow()
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
                        (x, w) =>
                            new ValueTuple<string, long>(
                                x.Cells[ChildKey].Value.TextValue,
                                w.RowNumber()
                            )
                    )
                    .GroupBy(x => x.Item2)
                    .Select(x => new ValueTuple<long, int>(x.Key, x.Count())),
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
                    .GroupBy(x => x.Window.RowNumber)
                    .Select(x => new ValueTuple<long, int>(x.Key, x.Count()))
        );
    }

    [Fact]
    public void JoinsTheRowsOfAWindowToRowsOfAnotherTable()
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
                        (x, w) => new ValueTuple<IRow, long>(x, w.RowNumber())
                    )
                    .Join(
                        db.Children,
                        x => x.Item1.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (ranked, child) =>
                            new ValueTuple<IRow, IRow, long>(
                                ranked.Item1,
                                child,
                                ranked.Item2
                            )
                    ),
            db =>
                db.Parents.AsEnumerable()
                    .OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select((x, index) => new ValueTuple<IRow, long>(x, index + 1))
                    .Join(
                        db.Children,
                        x => x.Item1.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (ranked, child) =>
                            new ValueTuple<IRow, IRow, long>(
                                ranked.Item1,
                                child,
                                ranked.Item2
                            )
                    )
        );
    }

    [Fact]
    public void GroupJoinsRowsToTheRowsOfAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.GroupJoin(
                    MarkerCalls.Over(db.Children, null, null, null, (x, w) => x),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) =>
                        new ValueTuple<string, int, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            children.Count(),
                            children.Sum(x => x.Cells[ChildLabel].Value.TextValue.Length)
                        )
                ),
            db =>
                db.Parents.GroupJoin(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) =>
                        new ValueTuple<string, int, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            children.Count(),
                            children.Sum(x => x.Cells[ChildLabel].Value.TextValue.Length)
                        )
                )
        );
    }

    [Fact]
    public void SumsTheCountOfTheCellsOfTheRowsOfAGroupJoinedWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.GroupJoin(
                    MarkerCalls.Over(db.Children, null, null, null, (x, w) => x),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) =>
                        new ValueTuple<string, int, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            children.Count(),
                            children.Sum(x => x.Cells.Count)
                        )
                ),
            db =>
                db.Parents.GroupJoin(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) =>
                        new ValueTuple<string, int, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            children.Count(),
                            children.Sum(x => x.Cells.Count)
                        )
                )
        );
    }

    [Fact]
    public void WindowsTheRowsOfATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.Concat(db.ArchivedParents),
                    x => x.Cells[ParentKey].Value.TextValue,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                            .ThenBy(x =>
                                long.Parse(x.Cells[ParentNumber].Value.TextValue)
                            ),
                    null,
                    (x, w) => new ValueTuple<IRow, long>(x, w.Rank())
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Parents.Concat(db.ArchivedParents),
                        x => x.Cells[ParentKey].Value.TextValue,
                        rows =>
                            rows.OrderBy(x =>
                                    ParsedOrNull(x.Cells[ParentId].Value.TextValue)
                                )
                                .ThenBy(x =>
                                    ParsedOrNull(x.Cells[ParentNumber].Value.TextValue)
                                ),
                        x =>
                            (
                                ParsedOrNull(x.Cells[ParentId].Value.TextValue),
                                ParsedOrNull(x.Cells[ParentNumber].Value.TextValue)
                            )
                    )
                    .Select(x => new ValueTuple<IRow, long>(x.Row, x.Window.Rank))
        );
    }

    [Fact]
    public void PartitionsAWindowByTheCountOfTheCells()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Children,
                    x => x.Cells.Count,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<string, long>(
                            x.Cells[ChildId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                db.Children.AsEnumerable()
                    .OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                    .Select(
                        (x, index) =>
                            new ValueTuple<string, long>(
                                x.Cells[ChildId].Value.TextValue,
                                index + 1
                            )
                    )
        );
    }

    [Fact]
    public void ReadsTheLastResultOfAnOrderedWindowByAnIndexFromTheEnd()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents,
                        null,
                        null,
                        null,
                        (x, w) => long.Parse(x.Cells[ParentId].Value.TextValue)
                    )
                    .OrderBy(x => x)
                    .ElementAt(^2),
            db =>
                db.Parents.Select(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .OrderBy(x => x)
                    .ElementAt(^2)
        );
    }

    [Fact]
    public void ReadsTheSingleResultOfAWindowOfOneRow()
    {
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "5"),
                        null,
                        null,
                        null,
                        (x, w) => new ValueTuple<IRow, long>(x, w.RowNumber())
                    )
                    .Single(),
            db =>
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "5")
                    .Select(x => new ValueTuple<IRow, long>(x, 1))
                    .Single()
        );
    }

    [Fact]
    public void ReadsTheCapturedValuesOfARecursionWhenItRuns()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        long depth = 1;
        IQueryable<WalkNode> walk = MarkerCalls.WithRecursive(
            db.Parents.Select(x => new WalkNode(
                long.Parse(x.Cells[ParentId].Value.TextValue),
                0,
                ""
            )),
            level =>
                level
                    .Where(x => x.Depth < depth)
                    .Select(x => new WalkNode(x.Id, x.Depth + 1, ""))
        );

        int shallow = walk.Count();
        depth = 3;
        int deep = walk.Count();
        long deepest = walk.Max(x => x.Depth);

        Assert.Equal(10, shallow);
        Assert.Equal(20, deep);
        Assert.Equal(3, deepest);
        Assert.Equal(3, connection.Statements.Count);
    }

    [Fact]
    public void ReadsTheCapturedValuesOfAWindowWhenItRuns()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        string key = "a";
        IQueryable<long> ranks = MarkerCalls.Over(
            db.Parents.Where(x => x.Cells[ParentKey].Value.TextValue == key),
            null,
            null,
            null,
            (x, w) => w.Rows.LongCount()
        );

        long[] first = [.. ranks];
        key = "b";
        long[] second = [.. ranks];

        Assert.Equal([2L, 2L], first);
        Assert.Equal([1L], second);
        Assert.Equal(2, connection.Statements.Count);
    }

    [Fact]
    public void KeepsTheTranslatorsExceptionOfARefusedWindow()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(
                db.Parents,
                null,
                null,
                null,
                (x, w) =>
                    w
                        .Rows.Select(row => row.Cells[ParentKey].Value.TextValue)
                        .Distinct()
                        .Count()
            )
        );

        Assert.Equal(
            "The query operator Over cannot be translated to SQL.",
            exception.Message
        );
        _ = Assert.IsType<NotSupportedException>(
            exception.InnerException,
            exactMatch: false
        );
    }

    [Fact]
    public void KeepsTheTranslatorsExceptionOfARefusedRecursion()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                db.Parents,
                level =>
                    level.Join(
                        level,
                        x => x.Cells[ParentId].Value.TextValue,
                        x => x.Cells[ParentNumber].Value.TextValue,
                        (one, other) => other
                    )
            )
        );

        Assert.Equal(
            "The query operator WithRecursive cannot be translated to SQL.",
            exception.Message
        );
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void ThrowsWhenTheStatementOfAWindowReturnsMoreColumnsThanItsResultHolds()
    {
        using RecordingConnection widening = new RecordingConnection(
            _fixture.Connection,
            sql => $"SELECT *, 1 FROM ({sql}) AS widened"
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, widening);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls
                .Over(
                    db.Parents,
                    null,
                    null,
                    null,
                    (x, w) => new ValueTuple<IRow, long>(x, w.RowNumber())
                )
                .ToList()
        );

        Assert.Equal(
            "The SQL of the query operator Over returns 5 columns where 4 are expected.",
            exception.Message
        );
        _ = Assert.Single(widening.Statements);
    }

    [Fact]
    public void ThrowsWhenTheStatementOfARecursionReturnsMoreColumnsThanItsRowsHold()
    {
        using RecordingConnection widening = new RecordingConnection(
            _fixture.Connection,
            sql => $"SELECT *, 1 FROM ({sql}) AS widened"
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, widening);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls
                .WithRecursive(
                    db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                    level =>
                        db.Parents.Join(
                            level,
                            x => x.Cells[ParentId].Value.TextValue,
                            x => x.Cells[ParentId].Value.TextValue == "2" ? "4" : "0",
                            (parent, before) => parent
                        )
                )
                .ToList()
        );

        Assert.Equal(
            "The SQL of the query operator WithRecursive returns 4 columns where 3 are "
                + "expected.",
            exception.Message
        );
        _ = Assert.Single(widening.Statements);
    }

    [Fact]
    public void ReadsRecordsOfInMemoryValuesJoinedToRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new LabelledRow(
                        label.Id,
                        label.Label,
                        row.Cells[Text].Value.TextValue
                    )
            )
        );
    }

    [Fact]
    public void ReadsInitializedObjectsOfInMemoryValuesJoinedToRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    Labels.AsQueryable(),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) =>
                        new RankedHolder
                        {
                            Id = label.Id,
                            Rank = row.Cells.Count,
                            Text = label.Label,
                        }
                )
                .Select(x => new ValueTuple<long, long, string?>(x.Id, x.Rank, x.Text))
        );
    }

    [Fact]
    public void ReadsTuplesOfAnInMemoryValueAndAStoredRow()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Join(
                Labels.AsQueryable(),
                x => long.Parse(x.Cells[ParentId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<string?, IRow, long>(label.Label, row, label.Id)
            )
        );
    }

    [Fact]
    public void LeftJoinsRowsToAnInMemorySequenceReadingTheOptionalLabel()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.GroupJoin(
                    Labels.AsQueryable(),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Id,
                    (row, labels) => new { Row = row, Labels = labels }
                )
                .SelectMany(
                    x => x.Labels.DefaultIfEmpty(),
                    (x, label) =>
                        new ValueTuple<IRow, long, string?>(x.Row, label.Id, label.Label)
                )
        );
    }

    [Fact]
    public void JoinsTheLevelsOfARecursionToAnInMemorySequence()
    {
        long[] ids = [1, 4, 91, 500];
        Expression<Func<IQueryable<long>, IQueryable<long>>> step = level =>
            level.Join(ids.AsQueryable(), x => x + 3, x => x, (x, id) => id);

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                    db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Where(x => x < 5),
                    step
                ),
            db =>
                Levels(
                    db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Where(x => x < 5),
                    step.Compile()
                )
        );
    }

    [Fact]
    public void PartitionsAWindowByMembershipInAnInMemorySequence()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values,
                    x =>
                        ids.AsQueryable()
                            .Contains(long.Parse(x.Cells[ValueId].Value.TextValue)),
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<string, long>(
                            x.Cells[ValueId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue)),
                        rows =>
                            rows.OrderBy(x =>
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
    public void FiltersRowsByAnInMemorySequenceCapturedWithAFilterOfStoredRows()
    {
        long[] ids = [1, 3, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
        {
            IQueryable<IRow> parents = db.Parents;
            IQueryable<long> known = ids.AsQueryable()
                .Where(id =>
                    parents.Any(x => long.Parse(x.Cells[ParentId].Value.TextValue) == id)
                );
            return db
                .Values.Where(row =>
                    known.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue);
        });
    }

    [Fact]
    public void ReadsTheGreatestNumberOfAWindowFromTheOneElementRoot()
    {
        int[] one = [0];

        Enumeration.AssertSameValue(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                return db
                    .Values.Provider.CreateQuery<long>(
                        one.AsQueryable()
                            .Select(x =>
                                values
                                    .Over(null, null, null, (row, w) => w.RowNumber())
                                    .Max()
                            )
                            .Expression
                    )
                    .Single();
            },
            db => db.Values.Count()
        );
    }

    [Fact]
    public void CountsACorrelatedWindowOfEachRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> children = db.Children;
                return db.Parents.Select(parent => new ValueTuple<string, long>(
                    parent.Cells[ParentId].Value.TextValue,
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
                            (x, w) => w.RowNumber()
                        )
                        .Sum()
                ));
            },
            db =>
                db.Parents.Select(parent => new ValueTuple<string, long>(
                    parent.Cells[ParentId].Value.TextValue,
                    db.Children.Where(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .AsEnumerable()
                        .Select((x, index) => (long)index + 1)
                        .Sum()
                ))
        );
    }

    [Fact]
    public void WindowsAPageOfTheSource()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Values.OrderByDescending(x =>
                            long.Parse(x.Cells[ValueId].Value.TextValue)
                        )
                        .Skip(2)
                        .Take(5),
                    null,
                    rows =>
                        rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<string, long>(
                            x.Cells[ValueId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                db.Values.AsEnumerable()
                    .OrderByDescending(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Skip(2)
                    .Take(5)
                    .OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Select(
                        (x, index) =>
                            new ValueTuple<string, long>(
                                x.Cells[ValueId].Value.TextValue,
                                index + 1
                            )
                    )
        );
    }

    [Fact]
    public void WindowsTheDistinctValuesOfTheSource()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Children.Select(x => x.Cells[ChildKey].Value.TextValue).Distinct(),
                    null,
                    null,
                    null,
                    (x, w) => new ValueTuple<string, long>(x, w.Rows.LongCount())
                ),
            db =>
                db.Children.Select(x => x.Cells[ChildKey].Value.TextValue)
                    .Distinct()
                    .AsEnumerable()
                    .Select(x => new ValueTuple<string, long>(
                        x,
                        db.Children.Select(row => row.Cells[ChildKey].Value.TextValue)
                            .Distinct()
                            .LongCount()
                    ))
        );
    }

    [Fact]
    public void ReadsTheOptionalRowsOfALeftJoinSelectedByAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.Parents.GroupJoin(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, children) =>
                                new { Parent = parent, Children = children }
                        )
                        .SelectMany(
                            x => x.Children.DefaultIfEmpty(),
                            (x, child) => child
                        ),
                    null,
                    null,
                    null,
                    (x, w) => x
                ),
            db =>
                db.Parents.GroupJoin(
                        db.Children,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, children) => new { Parent = parent, Children = children }
                    )
                    .SelectMany(x => x.Children.DefaultIfEmpty(), (x, child) => child)
        );
    }

    // The cells of a missing row are counted nowhere: in memory the row is null,
    // and in SQL the count is NULL, which no number is read from.
    [Fact]
    public void FailsOnTheCountOfTheCellsOfAMissingRowOfAWindow()
    {
        Enumeration.AssertFails<NullReferenceException, InvalidOperationException>(
            db =>
                MarkerCalls.Over(
                    db.Parents.GroupJoin(
                            db.Children,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ChildKey].Value.TextValue,
                            (parent, children) =>
                                new { Parent = parent, Children = children }
                        )
                        .SelectMany(
                            x => x.Children.DefaultIfEmpty(),
                            (x, child) => new ValueTuple<IRow, IRow?>(x.Parent, child)
                        ),
                    null,
                    null,
                    null,
                    (x, w) => x.Item2!.Cells.Count
                ),
            db =>
                db.Parents.GroupJoin(
                        db.Children,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ChildKey].Value.TextValue,
                        (parent, children) => new { Parent = parent, Children = children }
                    )
                    .SelectMany(
                        x => x.Children.DefaultIfEmpty(),
                        (x, child) => child!.Cells.Count
                    )
        );
    }

    [Fact]
    public void WindowsAJoinOfATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    db.ArchivedParents.Join(
                        db.Parents,
                        x => x.Cells[ParentKey].Value.TextValue,
                        x => x.Cells[ParentKey].Value.TextValue,
                        (archived, stored) => new ValueTuple<IRow, IRow>(archived, stored)
                    ),
                    x => x.Item1.Cells[ParentId].Value.TextValue,
                    rows =>
                        rows.OrderByDescending(x =>
                            long.Parse(x.Item2.Cells[ParentId].Value.TextValue)
                        ),
                    null,
                    (x, w) =>
                        new ValueTuple<string, string, long>(
                            x.Item1.Cells[ParentNumber].Value.TextValue,
                            x.Item2.Cells[ParentNumber].Value.TextValue
                                + x.Item2.Cells[ParentId].Value.TextValue,
                            w.RowNumber()
                        )
                ),
            db =>
                InMemoryWindow<ValueTuple<IRow, IRow>>
                    .Over(
                        db.ArchivedParents.Join(
                            db.Parents,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ParentKey].Value.TextValue,
                            (archived, stored) =>
                                new ValueTuple<IRow, IRow>(archived, stored)
                        ),
                        x => x.Item1.Cells[ParentId].Value.TextValue,
                        rows =>
                            rows.OrderByDescending(x =>
                                long.Parse(x.Item2.Cells[ParentId].Value.TextValue)
                            ),
                        x => long.Parse(x.Item2.Cells[ParentId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<string, string, long>(
                        x.Row.Item1.Cells[ParentNumber].Value.TextValue,
                        x.Row.Item2.Cells[ParentNumber].Value.TextValue
                            + x.Row.Item2.Cells[ParentId].Value.TextValue,
                        x.Window.RowNumber
                    ))
        );
    }

    [Fact]
    public void WalksStoredRowsFromAnInMemorySequenceOfScalars()
    {
        long[] starts = [1, 4];

        Enumeration.AssertSameElements(
            db =>
                db.Values.Provider.CreateQuery<long>(
                    MarkerCalls
                        .WithRecursive(
                            starts.AsQueryable(),
                            level =>
                                level
                                    .Where(x => x < 3)
                                    .Join(
                                        db.Parents,
                                        x => x + 1,
                                        x =>
                                            long.Parse(x.Cells[ParentId].Value.TextValue),
                                        (x, parent) =>
                                            long.Parse(
                                                parent.Cells[ParentId].Value.TextValue
                                            )
                                    )
                        )
                        .Expression
                ),
            db => [1, 4, 2]
        );
    }

    [Fact]
    public void InlinesAnInMemoryWindowCapturedByAStoredQuery()
    {
        (long Id, string Label)[] labels =
        [
            (1, "a"),
            (2, "b"),
            (3, "a"),
            (91, "a"),
            (6, "b"),
        ];
        IQueryable<ValueTuple<long, long>> ranked = MarkerCalls.Over(
            labels.AsQueryable(),
            x => x.Label,
            rows => rows.OrderByDescending(x => x.Id),
            null,
            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
        );

        Enumeration.AssertSameElements(
            db =>
                db.Values.Where(row =>
                        ranked.Any(x =>
                            x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                            && x.Item2 == 1
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue),
            db =>
                db.Values.Where(row =>
                        long.Parse(row.Cells[ValueId].Value.TextValue) == 91
                        || long.Parse(row.Cells[ValueId].Value.TextValue) == 6
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void JoinsRowsToAStoredQueryHeldByAConstant()
    {
        Enumeration.AssertSameElements(db =>
        {
            IQueryable<long> ids = db.Parents.Select(x =>
                long.Parse(x.Cells[ParentId].Value.TextValue)
            );
            IQueryable<IRow> values = db.Values;
            return values.Provider.CreateQuery<string>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Join),
                    [typeof(IRow), typeof(long), typeof(long), typeof(string)],
                    values.Expression,
                    Expression.Constant(ids, typeof(IQueryable<long>)),
                    Expression.Quote(
                        (Expression<Func<IRow, long>>)(
                            x => long.Parse(x.Cells[ValueId].Value.TextValue)
                        )
                    ),
                    Expression.Quote((Expression<Func<long, long>>)(x => x)),
                    Expression.Quote(
                        (Expression<Func<IRow, long, string>>)(
                            (row, id) => row.Cells[Text].Value.TextValue
                        )
                    )
                )
            );
        });
    }

    [Fact]
    public void WalksTheRowsOfARecursionOfAnonymousObjects()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        db.Parents.Select(x => new
                        {
                            Id = long.Parse(x.Cells[ParentId].Value.TextValue),
                            Depth = 0L,
                        }),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new { Id = x.Id * 2, Depth = x.Depth + 1 })
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                Levels(
                        db.Parents.Select(x => new
                        {
                            Id = long.Parse(x.Cells[ParentId].Value.TextValue),
                            Depth = 0L,
                        }),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new { Id = x.Id * 2, Depth = x.Depth + 1 })
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void ConcatenatesTwoWindowsInTheOrdersOfTheirSources()
    {
        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls
                    .Over(
                        db.Parents.OrderBy(x =>
                            long.Parse(x.Cells[ParentId].Value.TextValue)
                        ),
                        null,
                        null,
                        null,
                        (x, w) =>
                            new ValueTuple<string, long>(
                                x.Cells[ParentId].Value.TextValue,
                                w.Rows.LongCount()
                            )
                    )
                    .Concat(
                        MarkerCalls.Over(
                            db.Children.OrderByDescending(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            ),
                            null,
                            null,
                            null,
                            (x, w) =>
                                new ValueTuple<string, long>(
                                    x.Cells[ChildId].Value.TextValue,
                                    w.Rows.LongCount()
                                )
                        )
                    ),
            db =>
                db.Parents.OrderBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select(x => new ValueTuple<string, long>(
                        x.Cells[ParentId].Value.TextValue,
                        5
                    ))
                    .Concat(
                        db.Children.OrderByDescending(x =>
                                long.Parse(x.Cells[ChildId].Value.TextValue)
                            )
                            .Select(x => new ValueTuple<string, long>(
                                x.Cells[ChildId].Value.TextValue,
                                6
                            ))
                    )
        );
    }

    [Fact]
    public void UnitesTheResultsOfAWindowWithAnInMemorySequence()
    {
        long[] ids = [1, 2, 500, 3];

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
                        (x, w) => w.RowNumber()
                    )
                    .Union(ids.AsQueryable()),
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
                    .Select(x => x.Window.RowNumber)
                    .Union(ids)
        );
    }

    [Fact]
    public void ConcatenatesTheRowsOfTwoRecursionsOfOneTable()
    {
        Enumeration.AssertSameElements(
            db => Walk(db, "2").Concat(Walk(db, "1")),
            db =>
                db.Parents.Where(x =>
                        x.Cells[ParentId].Value.TextValue == "2"
                        || x.Cells[ParentId].Value.TextValue == "4"
                    )
                    .Concat(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "1")
                    )
        );
    }

    [Fact]
    public void IntersectsTheRowsOfAWindowWithTheRowsOfARecursion()
    {
        string[] walked = ["2", "4"];

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(db.Parents, null, null, null, (x, w) => x)
                    .Select(x => x.Cells[ParentId].Value.TextValue)
                    .Intersect(
                        Walk(db, "2").Select(x => x.Cells[ParentId].Value.TextValue)
                    ),
            db => walked.AsQueryable()
        );
    }

    /// <summary>
    /// The parent of the id and, when it is 2, the parent with the id 4.
    /// </summary>
    private IQueryable<IRow> Walk(ExecutionTables db, string id)
    {
        return MarkerCalls.WithRecursive(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == id),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentId].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue == "2" ? "4" : "0",
                    (parent, before) => parent
                )
        );
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

    private IQueryable<ValueTuple<IRow, IRow>> Joined(ExecutionTables db)
    {
        return db.Parents.Join(
            db.Children,
            x => x.Cells[ParentKey].Value.TextValue,
            x => x.Cells[ChildKey].Value.TextValue,
            (parent, child) => new ValueTuple<IRow, IRow>(parent, child)
        );
    }

    private static long? ParsedOrNull(string text)
    {
        return string.IsNullOrEmpty(text) ? null : long.Parse(text);
    }
}
