using System.Data;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

// The tests of enumerations that fail yet are skipped until they do not.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Schema = Schema.Schema;
using Table = Table.Table;

/// <summary>
/// Enumerating a stored table, synchronously or asynchronously, next to the queries
/// translated over the same connection.
/// </summary>
public sealed record StoredTableEnumerationTests : IClassFixture<ExecutionFixture>
{
    private const string QuotedSchema = "quo\"ted";

    private readonly ExecutionFixture _fixture;

    public StoredTableEnumerationTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildKey => _fixture.ChildKey;

    private PostgreSqlStoredSchemaDataSet Store =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Store, _fixture.Connection);

    [Fact]
    public void RunsAQueryWhileATableIsEnumerated()
    {
        IQueryable<IRow> children = Store[_fixture.Children];

        int matches = 0;

        foreach (IRow parent in Store[_fixture.Parents])
        {
            string key = parent.Cells[ParentKey].Value.TextValue;

            matches += children.Count(x => x.Cells[ChildKey].Value.TextValue == key);
        }

        Assert.Equal(MatchingChildren(), matches);
    }

    [Fact(Skip = "#80: The reader of an asynchronous enumeration stays open per row.")]
    public async Task RunsAQueryWhileATableIsEnumeratedAsynchronously()
    {
        IQueryable<IRow> children = Store[_fixture.Children];

        int matches = 0;

        await foreach (IRow parent in Store[_fixture.Parents])
        {
            string key = parent.Cells[ParentKey].Value.TextValue;

            matches += children.Count(x => x.Cells[ChildKey].Value.TextValue == key);
        }

        Assert.Equal(MatchingChildren(), matches);
    }

    [Fact(Skip = "#81: Asynchronous enumeration casts the connection to DbConnection.")]
    public async Task EnumeratesAsynchronouslyOverAConnectionThatIsNoDbConnection()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        int rows = 0;

        await foreach (
            IRow row in new PostgreSqlStoredSchemaDataSet(_fixture.Store, connection)[
                _fixture.Parents
            ]
        )
        {
            rows += row.Cells.Count / row.Cells.Count;
        }

        Assert.Equal(Store[_fixture.Parents].AsEnumerable().Count(), rows);
    }

    [Fact]
    public async Task StopsAsynchronousEnumerationWhenCancelled()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (
                IRow row in Store[_fixture.Parents].WithCancellation(cancellation.Token)
            )
            {
                Assert.NotNull(row);
            }
        });
    }

    [Fact]
    public async Task RunsAQueryAfterLeavingAnAsynchronousEnumerationEarly()
    {
        IQueryable<IRow> parents = Store[_fixture.Parents];

        await foreach (IRow row in Store[_fixture.Parents])
        {
            Assert.NotNull(row);
            break;
        }

        Assert.Equal(Store[_fixture.Parents].AsEnumerable().Count(), parents.Count());
    }

    [Fact]
    public async Task ReadsRowsOfATableWithoutColumns()
    {
        ITable blank = new Table(new String("Blank"), [], []);
        ISchema hollow = new Schema(new String("Hollow"), [blank], []);

        TestSchema.CreateNullable(_fixture.Connection, hollow);
        TestSchema.InsertDefaults(_fixture.Connection, hollow, blank);
        TestSchema.InsertDefaults(_fixture.Connection, hollow, blank);

        IStoredTableDataSet rows = new PostgreSqlStoredSchemaDataSet(
            hollow,
            _fixture.Connection
        )[blank];

        IQueryable<IRow> query = rows;

        Assert.Equal(2, rows.AsEnumerable().Count());
        Assert.Equal(2, await rows.CountAsync());
        Assert.Equal(2, query.Count());
        Assert.All(query.Where(x => x.Cells.Count == 0), x => Assert.Empty(x.Cells));
    }

    [Fact]
    public void TranslatesQueriesOverASchemaNamedWithAQuote()
    {
        IQueryable<IRow> rows = Quoted();

        Assert.All(
            rows.Where(x => x.Cells[ParentKey].Value.TextValue != ""),
            x => Assert.Equal("quoted", x.Cells[ParentKey].Value.TextValue)
        );
        Assert.True(rows.Any(x => x.Cells[ParentKey].Value.TextValue == "quoted"));
    }

    [Fact(Skip = "#82: The statement of an enumeration does not escape the schema name.")]
    public void EnumeratesATableOfASchemaNamedWithAQuote()
    {
        IRow[] rows = [.. Quoted().AsEnumerable()];

        Assert.NotEmpty(rows);
        Assert.All(rows, x => Assert.Equal("quoted", x.Cells[ParentKey].Value.TextValue));
    }

    /// <summary>
    /// The children whose key a parent has, each counted once per such parent.
    /// </summary>
    private int MatchingChildren()
    {
        IRow[] children = [.. Store[_fixture.Children].AsEnumerable()];

        return Store[_fixture.Parents]
            .AsEnumerable()
            .Sum(parent =>
                children.Count(child =>
                    child.Cells[ChildKey].Value.TextValue
                    == parent.Cells[ParentKey].Value.TextValue
                )
            );
    }

    /// <summary>
    /// A table of a schema whose name, given as it is, holds a double quote.
    /// </summary>
    private PostgreSqlStoredTableDataSet Quoted()
    {
        ITable table = new Table(new String("Quoted"), [ParentKey], []);

        string prefix = $"\"{QuotedSchema.Replace("\"", "\"\"")}\".";

        using (IDbCommand command = _fixture.Connection.CreateCommand())
        {
            command.CommandText = $"CREATE SCHEMA IF NOT EXISTS {prefix[..^1]};";

            _ = command.ExecuteNonQuery();
        }

        TestSchema.CreateTable(_fixture.Connection, prefix, table);
        TestSchema.Insert(_fixture.Connection, prefix, table, ["'quoted'"]);

        return new PostgreSqlStoredTableDataSet(
            table,
            new String(QuotedSchema),
            _fixture.Connection
        );
    }
}
