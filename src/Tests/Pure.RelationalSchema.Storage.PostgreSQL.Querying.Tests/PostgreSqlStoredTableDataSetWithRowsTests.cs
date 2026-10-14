using System.Data;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record PostgreSqlStoredTableDataSetWithRowsTests
    : IClassFixture<DatabaseFixture>
{
    private const int MaxIdentifierLength = 63;

    private readonly DatabaseFixture _fixture;

    public PostgreSqlStoredTableDataSetWithRowsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;

        Seed(
            _fixture.Schema.Tables.First(),
            ["'true'", "'2020-01-01 00:00:00'", "'1'", "'2'"],
            ["'false'", "'2021-01-01 00:00:00'", "'3'", "'4'"]
        );

        Seed(_fixture.Schema.Tables.Last(), ["'text'", "'5'", "'6'", "'7'", "'abc'"]);
    }

    [Fact]
    public void EnumeratesRowsSynchronously()
    {
        ITable table = _fixture.Schema.Tables.First();

        IRow[] rows = [.. new PostgreSqlStoredSchemaDataSet(
            _fixture.Schema,
            _fixture.Connection
        )[table]];

        Assert.Equal(2, rows.Length);
    }

    [Fact]
    public async Task EnumeratesRowsAsynchronously()
    {
        IRow[] rows = await new PostgreSqlStoredTableDataSet(
            _fixture.Schema.Tables.First(),
            _fixture.Schema,
            _fixture.Connection
        ).ToArrayAsync();

        Assert.Equal(2, rows.Length);
    }

    [Fact]
    public void ReadsCellValuesSynchronously()
    {
        ITable table = _fixture.Schema.Tables.First();

        IColumn intColumn = table.Columns.Skip(2).First();

        IEnumerable<string> values = new PostgreSqlStoredTableDataSet(
            table,
            _fixture.Schema,
            _fixture.Connection
        )
            .AsEnumerable()
            .Select(x => x.Cells[intColumn].Value.TextValue);

        Assert.Equal(["1", "3"], values.Order());
    }

    [Fact]
    public async Task ReadsCellValuesAsynchronously()
    {
        ITable table = _fixture.Schema.Tables.First();

        IColumn longColumn = table.Columns.Last();

        IRow[] rows = await new PostgreSqlStoredTableDataSet(
            table,
            _fixture.Schema,
            _fixture.Connection
        ).ToArrayAsync();

        Assert.Equal(["2", "4"], rows.Select(x => x.Cells[longColumn].Value.TextValue).Order());
    }

    [Fact]
    public void ReadsBinaryCellAsText()
    {
        ITable table = _fixture.Schema.Tables.Last();

        IColumn hashColumn = table.Columns.Last();

        IRow row = new PostgreSqlStoredTableDataSet(
            table,
            _fixture.Schema,
            _fixture.Connection
        )
            .AsEnumerable()
            .Single();

        Assert.Equal("abc", row.Cells[hashColumn].Value.TextValue);
    }

    [Fact]
    public async Task ReadsBinaryCellAsTextAsynchronously()
    {
        ITable table = _fixture.Schema.Tables.Last();

        IColumn hashColumn = table.Columns.Last();

        IRow row = (
            await new PostgreSqlStoredTableDataSet(
                table,
                _fixture.Schema,
                _fixture.Connection
            ).ToArrayAsync()
        )
            .AsEnumerable()
            .Single();

        Assert.Equal("abc", row.Cells[hashColumn].Value.TextValue);
    }

    [Fact]
    public async Task ConnectionStaysUsableAfterAsyncEnumeration()
    {
        ITable table = _fixture.Schema.Tables.First();

        PostgreSqlStoredTableDataSet dataSet = new(
            table,
            _fixture.Schema,
            _fixture.Connection
        );

        _ = await dataSet.ToArrayAsync();

        Assert.Equal(2, dataSet.AsEnumerable().Count());
        Assert.Equal(ConnectionState.Open, _fixture.Connection.State);
    }

    private void Seed(ITable table, params string[][] rows)
    {
        using IDbCommand command = _fixture.Connection.CreateCommand();

        command.CommandText =
            $"TRUNCATE {QualifiedTable(table)}; "
            + $"INSERT INTO {QualifiedTable(table)} ({ColumnNames(table)}) VALUES "
            + string.Join(", ", rows.Select(x => $"({string.Join(", ", x)})"))
            + ";";

        _ = command.ExecuteNonQuery();
    }

    private string QualifiedTable(ITable table)
    {
        return $"{Quote(new HexString(new SchemaHash(_fixture.Schema)).TextValue)}"
            + $".{Quote(new HexString(new TableHash(table)).TextValue)}";
    }

    private static string ColumnNames(ITable table)
    {
        return string.Join(
            ", ",
            table.Columns.Select(x => Quote(new HexString(new ColumnHash(x)).TextValue))
        );
    }

    private static string Quote(string identifier)
    {
        return $"\"{identifier[..Math.Min(identifier.Length, MaxIdentifierLength)]}\"";
    }
}
