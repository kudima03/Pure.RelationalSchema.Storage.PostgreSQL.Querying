using System.Collections;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record PostgreSqlStoredSchemaDataSetTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public PostgreSqlStoredSchemaDataSetTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CountEqualsTablesCount()
    {
        Assert.Equal(
            2,
            new PostgreSqlStoredSchemaDataSet(_fixture.Schema, _fixture.Connection).Count
        );
    }

    [Fact]
    public void KeysAreSchemaTables()
    {
        Assert.Equal(
            _fixture.Schema.Tables,
            new PostgreSqlStoredSchemaDataSet(
                _fixture.Schema,
                _fixture.Connection
            ).Keys
        );
    }

    [Fact]
    public void ValuesCorrespondToTables()
    {
        Assert.Equal(
            _fixture.Schema.Tables,
            new PostgreSqlStoredSchemaDataSet(
                _fixture.Schema,
                _fixture.Connection
            ).Values.Select(x => x.TableSchema)
        );
    }

    [Fact]
    public void ContainsKeyForSchemaTables()
    {
        PostgreSqlStoredSchemaDataSet dataSet = new(_fixture.Schema, _fixture.Connection);

        Assert.All(_fixture.Schema.Tables, x => Assert.True(dataSet.ContainsKey(x)));
    }

    [Fact]
    public void TryGetValueFindsSchemaTables()
    {
        ITable table = _fixture.Schema.Tables.First();

        bool found = new PostgreSqlStoredSchemaDataSet(
            _fixture.Schema,
            _fixture.Connection
        ).TryGetValue(table, out IStoredTableDataSet? dataSet);

        Assert.True(found);
        Assert.Equal(table, dataSet?.TableSchema);
    }

    [Fact]
    public void ExposesConnection()
    {
        Assert.Same(
            _fixture.Connection,
            new PostgreSqlStoredSchemaDataSet(
                _fixture.Schema,
                _fixture.Connection
            ).Connection
        );
    }

    [Fact]
    public void ExposesSchema()
    {
        Assert.Same(
            _fixture.Schema,
            new PostgreSqlStoredSchemaDataSet(_fixture.Schema, _fixture.Connection).Schema
        );
    }

    [Fact]
    public void EnumeratesAsTyped()
    {
        IEnumerable<KeyValuePair<ITable, IStoredTableDataSet>> dataSet =
            new PostgreSqlStoredSchemaDataSet(_fixture.Schema, _fixture.Connection);

        Assert.Equal(2, dataSet.ToArray().Length);
    }

    [Fact]
    public void EnumeratesAsUntyped()
    {
        IEnumerable dataSet = new PostgreSqlStoredSchemaDataSet(
            _fixture.Schema,
            _fixture.Connection
        );

        int count = 0;

        foreach (object _ in dataSet)
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void TableDataSetsAreEmptyWithoutInsertedRows()
    {
        PostgreSqlStoredSchemaDataSet dataSet = new(_fixture.Schema, _fixture.Connection);

        Assert.All(dataSet.Values, x => Assert.Empty(x.AsEnumerable()));
    }
}
