using System.Collections;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The roots a translated query may read: stored tables of one connection, by
/// reference, and in-memory sequences beside them, whatever operator or PostgreSQL
/// marker reads them, and the set operations over rows (<c>UnionBy</c> and the
/// union of the levels of a recursion among them), which must combine rows of one
/// table: every other root is refused before any statement runs.
/// </summary>
public sealed record ProviderRootTests : IClassFixture<ExecutionFixture>
{
    private static readonly long[] Ids = [1, 4, 91, 500];

    private readonly ExecutionFixture _fixture;

    public ProviderRootTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn EntryId => _fixture.EntryId;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void RefusesAWindowPartitionedByATableOfAnotherConnection()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> children = ExecutionTables.Stored(_fixture, other).Children;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls
                .Over(
                    ExecutionTables.Stored(_fixture, one).Values,
                    x => children.Count(),
                    null,
                    null,
                    (x, w) => x.Cells[ValueId].Value.TextValue
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

    [Fact]
    public void RefusesAWindowOrderedByATableOfAnotherConnection()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> children = ExecutionTables.Stored(_fixture, other).Children;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls
                .Over(
                    ExecutionTables.Stored(_fixture, one).Values,
                    null,
                    rows => rows.OrderBy(x => children.Count()),
                    null,
                    (x, w) => w.RowNumber()
                )
                .ToList()
        );

        Assert.Contains(
            "more than one connection",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    [Fact]
    public void RefusesATableOfASchemaDataSetOfAnotherConnection()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        PostgreSqlStoredSchemaDataSet store = new PostgreSqlStoredSchemaDataSet(
            _fixture.Store,
            other
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            ExecutionTables
                .Stored(_fixture, one)
                .Values.Where(x =>
                    store[_fixture.Parents]
                        .Any(p =>
                            p.Cells[ParentId].Value.TextValue
                            == x.Cells[ValueId].Value.TextValue
                        )
                )
                .Select(x => x.Cells[Text].Value.TextValue)
                .ToList()
        );

        Assert.Contains(
            "more than one connection",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    [Fact]
    public void RefusesTwoConnectionsWhenAnInMemorySequenceIsReadToo()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> parents = ExecutionTables.Stored(_fixture, other).Parents;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls
                .Over(
                    ExecutionTables.Stored(_fixture, one).Values,
                    null,
                    null,
                    null,
                    (x, w) => Ids.AsQueryable().Any(id => parents.Any())
                )
                .ToList()
        );

        Assert.Contains(
            "more than one connection",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    [Fact]
    public void ReadsTwoDataSetsOfOneConnectionInOneStatement()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        IQueryable<IRow> values = new PostgreSqlStoredSchemaDataSet(
            _fixture.Store,
            connection
        )[_fixture.Values];
        IQueryable<IRow> parents = new PostgreSqlStoredSchemaDataSet(
            _fixture.Store,
            connection
        )[_fixture.Parents];

        long[] ranks =
        [
            .. MarkerCalls.Over(
                values.Where(x =>
                    parents.Any(p =>
                        p.Cells[ParentId].Value.TextValue
                        == x.Cells[ValueId].Value.TextValue
                    )
                ),
                null,
                rows => rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
                null,
                (x, w) => w.RowNumber()
            ),
        ];

        Assert.Equal([1L, 2L, 3L, 4L], ranks.Order());
        _ = Assert.Single(connection.Statements);
    }

    [Fact]
    public void RefusesAWindowReadingAForeignQuery()
    {
        IQueryable<IRow> foreign = new ForeignRows();

        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(db.Values, null, null, null, (x, w) => foreign.Count())
        );

        Assert.Equal(
            "The query operator Over reads sources other than stored tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesARecursionSeededByAForeignQuery()
    {
        IQueryable<IRow> foreign = new ForeignRows();

        NotSupportedException exception = Enumeration.AssertRefused(db =>
            db.Values.Provider.CreateQuery<IRow>(
                MarkerCalls
                    .WithRecursive(
                        new EnumerableQuery<IRow>(foreign.Expression),
                        level =>
                            db.Parents.Join(
                                level,
                                x => x.Cells[ParentKey].Value.TextValue,
                                x => x.Cells[ParentId].Value.TextValue,
                                (parent, before) => parent
                            )
                    )
                    .Expression
            )
        );

        Assert.Equal(
            "The query operator WithRecursive reads sources other than stored tables, so "
                + "it cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesAWindowOverAnInMemorySequenceOnly()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            db.Values.Provider.CreateQuery<long>(
                MarkerCalls.OverCall<long, long>(
                    Ids.AsQueryable().Expression,
                    null,
                    rows => rows.OrderBy(x => x),
                    null,
                    (x, w) => w.RowNumber()
                )
            )
        );

        Assert.Equal(
            "The query operator Over reads sources other than stored tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesARecursionOverAnInMemorySequenceOnly()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            db.Values.Provider.CreateQuery<long>(
                MarkerCalls
                    .WithRecursive(
                        Ids.AsQueryable(),
                        level => level.Where(x => x < 3).Select(x => x + 1)
                    )
                    .Expression
            )
        );

        Assert.Equal(
            "The query operator WithRecursive reads sources other than stored tables, so "
                + "it cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesAWindowOverEnumeratedRowsBesideAStoredTable()
    {
        IRow[] rows = [.. ExecutionTables.Enumerated(_fixture).Values];

        _ = Enumeration.AssertRefused(db =>
            db.Values.Provider.CreateQuery<int>(
                MarkerCalls.OverCall<IRow, int>(
                    rows.AsQueryable().Expression,
                    null,
                    null,
                    null,
                    (x, w) => db.Parents.Count()
                )
            )
        );
    }

    [Fact]
    public void RefusesEnumeratedRowsConcatenatedToTheResultsOfAWindow()
    {
        IRow[] rows = [.. ExecutionTables.Enumerated(_fixture).Values];

        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls
                .Over(db.Values, null, null, null, (x, w) => x)
                .Concat(rows.AsQueryable())
        );

        Assert.Equal(
            "The query operator Concat combines rows of different tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesTheRowsOfAWindowCombinedWithRowsOfAnotherTable()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.Over(db.Parents, null, null, null, (x, w) => x).Union(db.Children)
        );

        Assert.Equal(
            "The query operator Union combines rows of different tables, so it cannot be "
                + "translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesTheRowsOfARecursionCombinedWithRowsOfAnotherTable()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls
                .WithRecursive(
                    db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                    level =>
                        db.Parents.Join(
                            level,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ParentId].Value.TextValue,
                            (parent, before) => parent
                        )
                )
                .Concat(db.Children)
        );

        Assert.Equal(
            "The query operator Concat combines rows of different tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void ConcatenatesTheRowsOfAWindowWithTheTableOfAnotherSchema()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .Over(db.Parents, null, null, null, (x, w) => x)
                    .Concat(db.ArchivedParents),
            db => db.Parents.Concat(db.ArchivedParents)
        );
    }

    [Fact]
    public void ConcatenatesTheRowsOfARecursionWithTheTableOfAnotherSchema()
    {
        Enumeration.AssertSameElements(
            db =>
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
                    .Concat(db.ArchivedParents),
            db =>
                db.Parents.Where(x =>
                        x.Cells[ParentId].Value.TextValue == "2"
                        || x.Cells[ParentId].Value.TextValue == "4"
                    )
                    .Concat(db.ArchivedParents)
        );
    }

    [Fact]
    public void RefusesTheRowsOfATableUnitedByKeyWithRowsOfAnotherTable()
    {
        _ = Enumeration.AssertRefused(db =>
            db.Parents.UnionBy(db.Entries, x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesTheRowsOfATableUnitedByKeyWithRowsOfAWiderTable()
    {
        _ = Enumeration.AssertRefused(db =>
            db.Parents.UnionBy(db.Children, x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void UnitesByKeyTheRowsOfATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.UnionBy(db.ArchivedParents, x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesARecursionWhoseStepReadsRowsOfAnotherTable()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                level =>
                    db.Children.Where(x => x.Cells[ChildId].Value.TextValue == "5")
                        .Join(
                            level,
                            x => "x",
                            x => x.Cells[ParentKey].Value.TextValue == "b" ? "x" : "y",
                            (child, before) => child
                        )
            )
        );
    }

    [Fact]
    public void RefusesARecursionWhoseStepReadsRowsOfATableOfTheSameWidth()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                level =>
                    db.Entries.Where(x => x.Cells[EntryId].Value.TextValue == "1")
                        .Join(
                            level,
                            x => "x",
                            x => x.Cells[ParentKey].Value.TextValue == "b" ? "x" : "y",
                            (entry, before) => entry
                        )
            )
        );
    }

    [Fact]
    public void WalksTheRowsOfATableFromTheTableOfAnotherSchema()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                    db.ArchivedParents.Where(x =>
                        x.Cells[ParentId].Value.TextValue == "7"
                    ),
                    level =>
                        db.Parents.Join(
                            level,
                            x => x.Cells[ParentKey].Value.TextValue,
                            x => x.Cells[ParentId].Value.TextValue == "7" ? "a" : "none",
                            (parent, before) => parent
                        )
                ),
            db =>
                db.ArchivedParents.Where(x => x.Cells[ParentId].Value.TextValue == "7")
                    .Concat(
                        db.Parents.Where(x => x.Cells[ParentKey].Value.TextValue == "a")
                    )
        );
    }

    [Fact]
    public void ExecutesAWindowThroughTheUntypedProvider()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        MethodCallExpression over = MarkerCalls.OverCall<IRow, long>(
            db.Values.Expression,
            null,
            rows => rows.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
            null,
            (x, w) => w.RowNumber()
        );

        IQueryable untyped = db.Values.Provider.CreateQuery(over);
        long[] numbers = [.. untyped.Cast<long>()];
        object? count = db.Values.Provider.Execute(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Count),
                [typeof(long)],
                over
            )
        );
        IEnumerable<object?> values = db.Values.Provider.Execute<IEnumerable<object?>>(
            over
        );

        Assert.Equal(typeof(long), untyped.ElementType);
        Assert.Equal(Enumerable.Range(1, 13).Select(x => (long)x), numbers.Order());
        Assert.Equal(13, count);
        Assert.Equal(13, values.Count());
        Assert.Equal(13, Count(untyped));
        Assert.Equal(4, connection.Statements.Count);
    }

    [Fact]
    public void ExecutesTheRowsOfARecursionThroughTheProvider()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        IQueryable<IRow> walk = MarkerCalls.WithRecursive(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentId].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue == "2" ? "4" : "0",
                    (parent, before) => parent
                )
        );

        IRow[] rows = [.. db.Values.Provider.Execute<IEnumerable<IRow>>(walk.Expression)];
        object? first = db.Values.Provider.Execute(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.First),
                [typeof(IRow)],
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Where),
                    [typeof(IRow)],
                    walk.Expression,
                    Expression.Quote(
                        (Expression<Func<IRow, bool>>)(
                            x => x.Cells[ParentId].Value.TextValue == "4"
                        )
                    )
                )
            )
        );

        Assert.Equal(
            ["2", "4"],
            rows.Select(x => x.Cells[ParentId].Value.TextValue)
                .Order(StringComparer.Ordinal)
        );
        Assert.Equal(
            "4",
            Assert.IsType<IRow>(first, exactMatch: false).Cells[ParentId].Value.TextValue
        );
        Assert.Equal(2, connection.Statements.Count);
    }

    [Fact]
    public void JoinsATableToTheResultsOfAWindowOverAnInMemorySequence()
    {
        (long Id, string Label)[] labels = [(1, "a"), (2, "b"), (3, "a"), (91, "a")];

        Enumeration.AssertSameElements(
            db =>
                db.Values.Join(
                    db.Values.Provider.CreateQuery<ValueTuple<long, long>>(
                        MarkerCalls.OverCall<
                            (long Id, string Label),
                            ValueTuple<long, long>
                        >(
                            labels.AsQueryable().Expression,
                            x => x.Label,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new ValueTuple<long, long>(x.Id, w.RowNumber())
                        )
                    ),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Item1,
                    (row, ranked) =>
                        new ValueTuple<string, long>(
                            row.Cells[Text].Value.TextValue,
                            ranked.Item2
                        )
                ),
            db =>
                db.Values.Join(
                    InMemoryWindow<(long Id, string Label)>
                        .Over(
                            labels,
                            x => x.Label,
                            rows => rows.OrderBy(x => x.Id),
                            x => x.Id
                        )
                        .Select(x => new ValueTuple<long, long>(
                            x.Row.Id,
                            x.Window.RowNumber
                        )),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Item1,
                    (row, ranked) =>
                        new ValueTuple<string, long>(
                            row.Cells[Text].Value.TextValue,
                            ranked.Item2
                        )
                )
        );
    }

    [Fact]
    public void RefusesTheRowsOfAWindowIntersectedWithRowsOfAnotherTable()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls
                .Over(db.Children, null, null, null, (x, w) => x)
                .Intersect(db.Parents)
        );

        Assert.Equal(
            "The query operator Intersect combines rows of different tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    private static int Count(IEnumerable sequence)
    {
        int count = 0;
        foreach (object? _ in sequence)
        {
            count++;
        }
        return count;
    }

    private sealed class ForeignRows : IQueryable<IRow>
    {
        public Type ElementType => typeof(IRow);

        public Expression Expression =>
            Expression.Constant(this, typeof(IQueryable<IRow>));

        public IQueryProvider Provider => throw new NotSupportedException();

        public IEnumerator<IRow> GetEnumerator()
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
