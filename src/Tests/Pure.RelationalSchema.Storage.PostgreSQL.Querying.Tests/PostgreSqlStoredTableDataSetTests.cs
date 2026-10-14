using System.Collections;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record PostgreSqlStoredTableDataSetTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public PostgreSqlStoredTableDataSetTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void EnumeratesAsTyped()
    {
        Assert.Empty(
            new PostgreSqlStoredTableDataSet(
                _fixture.Schema.Tables.First(),
                _fixture.Schema,
                _fixture.Connection
            ).AsEnumerable()
        );
    }

    [Fact]
    public void EnumeratesAsUntyped()
    {
        ICollection<IRow> list = [];

        IEnumerable rows = new PostgreSqlStoredTableDataSet(
            _fixture.Schema.Tables.First(),
            _fixture.Schema,
            _fixture.Connection
        );

        foreach (object obj in rows)
        {
            list.Add((IRow)obj);
        }

        Assert.Empty(list);
    }

    [Fact]
    public async Task EnumeratesAsync()
    {
        Assert.Empty(
            await new PostgreSqlStoredTableDataSet(
                _fixture.Schema.Tables.First(),
                _fixture.Schema,
                _fixture.Connection
            ).ToArrayAsync()
        );
    }

    [Fact]
    public void TableSchemaInitializeCorrectly()
    {
        Assert.True(
            new TableHash(_fixture.Schema.Tables.First()).SequenceEqual(
                new TableHash(
                    new PostgreSqlStoredTableDataSet(
                        _fixture.Schema.Tables.First(),
                        _fixture.Schema,
                        _fixture.Connection
                    ).TableSchema
                )
            )
        );
    }
}
