using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The sources of a translated query: a table of two schemas, tables captured by
/// the lambdas of a query, and the sides of a set operation.
/// </summary>
public sealed record QuerySourceTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public QuerySourceTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn EntryKey => _fixture.EntryKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    private PostgreSqlStoredSchemaDataSet Store =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Store, _fixture.Connection);

    [Fact]
    public void JoinsATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Join(
                db.ArchivedParents,
                x => x.Cells[ParentId].Value.TextValue,
                x => x.Cells[ParentId].Value.TextValue,
                (stored, archived) =>
                    new ValueTuple<string, string, long>(
                        stored.Cells[ParentKey].Value.TextValue,
                        archived.Cells[ParentKey].Value.TextValue,
                        long.Parse(archived.Cells[ParentNumber].Value.TextValue)
                    )
            )
        );
    }

    [Fact]
    public void ReadsRowsOfATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Join(
                db.ArchivedParents,
                x => long.Parse(x.Cells[ParentId].Value.TextValue),
                x => long.Parse(x.Cells[ParentId].Value.TextValue),
                (stored, archived) => new ValueTuple<IRow, IRow>(stored, archived)
            )
        );
    }

    [Fact]
    public void ConcatenatesRowsOfATableOfTwoSchemas()
    {
        Enumeration.AssertSameElements(db => db.Parents.Concat(db.ArchivedParents));
    }

    [Fact]
    public void FiltersRowsByTheTableOfAnotherSchema()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(stored =>
                    db.ArchivedParents.Any(archived =>
                        archived.Cells[ParentKey].Value.TextValue
                        == stored.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsCapturedValuesWhenTheQueryRuns()
    {
        string key = "a";

        IQueryable<IRow> parents = Store[_fixture.Parents];

        IQueryable<string> ids = parents
            .Where(x => x.Cells[ParentKey].Value.TextValue == key)
            .Select(x => x.Cells[ParentId].Value.TextValue);

        string[] first = [.. ids.AsEnumerable().Order(StringComparer.Ordinal)];

        key = "b";

        Assert.Equal(["1", "5"], first);
        Assert.Equal(["2"], ids);
    }

    [Fact]
    public void InlinesTablesCapturedAsStoredTableDataSets()
    {
        IQueryable<IRow> parents = Store[_fixture.Parents];
        IStoredTableDataSet children = Store[_fixture.Children];

        Assert.Equal(
            ParentsWithChildren(),
            parents
                .Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void InlinesTablesCapturedAsEnumerables()
    {
        IQueryable<IRow> parents = Store[_fixture.Parents];
        IEnumerable<IRow> children = Store[_fixture.Children];

        Assert.Equal(
            ParentsWithChildren(),
            parents
                .Where(parent =>
                    children
                        .AsQueryable()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void InlinesTablesReadFromACapturedSchemaDataSet()
    {
        PostgreSqlStoredSchemaDataSet store = Store;
        IQueryable<IRow> parents = store[_fixture.Parents];

        Assert.Equal(
            ParentsWithChildren(),
            parents
                .Where(parent =>
                    store[_fixture.Children]
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void InlinesQueriesReturnedByMethodsInsideLambdas()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    Named(db.Children)
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void ThrowsWhenASetOperationCombinesValueTuplesOfRowsOfDifferentTables()
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        IQueryable<ValueTuple<IRow, string>> combined = db
            .Parents.Select(x => new ValueTuple<IRow, string>(x, "parent"))
            .Concat(db.Entries.Select(x => new ValueTuple<IRow, string>(x, "entry")));

        _ = Assert.Throws<NotSupportedException>(() => combined.ToList());
    }

    [Fact]
    public void ThrowsWhenASetOperationCombinesObjectsOfRowsOfDifferentTables()
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        IQueryable<ValueTuple<IRow, string>> combined = db
            .Parents.Select(x => new { Row = x, Source = "parent" })
            .Concat(db.Entries.Select(x => new { Row = x, Source = "entry" }))
            .Select(x => new ValueTuple<IRow, string>(x.Row, x.Source));

        _ = Assert.Throws<NotSupportedException>(() => combined.ToList());
    }

    [Fact]
    public void ThrowsWhenASetOperationCombinesInitializedObjectsOfRowsOfDifferentTables()
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        IQueryable<Holder> combined = db
            .Parents.Select(x => new Holder { Row = x })
            .Concat(db.Entries.Select(x => new Holder { Row = x }));

        _ = Assert.Throws<NotSupportedException>(() => combined.ToList());
    }

    [Fact]
    public void CombinesTextOfDifferentTablesInSetOperationsOfValueTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new ValueTuple<string, string>(
                    x.Cells[ParentKey].Value.TextValue,
                    "parent"
                ))
                .Concat(
                    db.Entries.Select(x => new ValueTuple<string, string>(
                        x.Cells[EntryKey].Value.TextValue,
                        "entry"
                    ))
                )
        );
    }

    private sealed record Holder
    {
        public IRow? Row { get; set; }
    }

    /// <summary>
    /// The ids of the parents whose key a child has, from the enumerated rows.
    /// </summary>
    private string[] ParentsWithChildren()
    {
        IRow[] children = [.. Store[_fixture.Children].AsEnumerable()];

        return
        [
            .. Store[_fixture.Parents]
                .AsEnumerable()
                .Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The rows that have an id, which all have: the query a method returns.
    /// </summary>
    private IQueryable<IRow> Named(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[ChildId].Value.TextValue != "");
    }
}
