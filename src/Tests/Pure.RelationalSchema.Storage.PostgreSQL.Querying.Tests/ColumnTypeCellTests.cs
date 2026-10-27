using System.Data;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Column = Column.Column;
using Schema = Schema.Schema;
using Table = Table.Table;

/// <summary>
/// Cells of every column type can be read in a query, as one statement, with the
/// text enumerating the table gives them; a column of an unknown type names itself
/// when its cells cannot be.
/// </summary>
public sealed record ColumnTypeCellTests : IClassFixture<ColumnTypeFixture>
{
    private readonly ColumnTypeFixture _fixture;

    public ColumnTypeCellTests(ColumnTypeFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("Day")]
    [InlineData("Clock")]
    [InlineData("Measure")]
    [InlineData("Ratio")]
    [InlineData("Digest")]
    public void EnumeratesCellsOfTheColumn(string name)
    {
        IColumn column = Column(name);

        Assert.All(
            Table(_fixture.Connection),
            x => Assert.NotEmpty(x.Cells[column].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsDateCellsAsTheirText()
    {
        AssertLikeEnumeration(rows => Texts(rows, _fixture.Day));
    }

    [Fact]
    public void FiltersByDateCellText()
    {
        AssertLikeEnumeration(rows => Ids(rows, _fixture.Day, Text(_fixture.Day)));
    }

    [Fact]
    public void ReadsTimeCellsAsTheirText()
    {
        AssertLikeEnumeration(rows => Texts(rows, _fixture.Clock));
    }

    [Fact]
    public void FiltersByTimeCellText()
    {
        AssertLikeEnumeration(rows => Ids(rows, _fixture.Clock, Text(_fixture.Clock)));
    }

    [Fact]
    public void ReadsDoubleCellsAsTheirText()
    {
        AssertLikeEnumeration(rows => Texts(rows, _fixture.Measure));
    }

    [Fact]
    public void FiltersByDoubleCellText()
    {
        AssertLikeEnumeration(rows =>
            Ids(rows, _fixture.Measure, Text(_fixture.Measure))
        );
    }

    [Fact]
    public void ReadsFloatCellsAsTheirText()
    {
        AssertLikeEnumeration(rows => Texts(rows, _fixture.Ratio));
    }

    [Fact]
    public void FiltersByFloatCellText()
    {
        AssertLikeEnumeration(rows => Ids(rows, _fixture.Ratio, Text(_fixture.Ratio)));
    }

    [Fact]
    public void ReadsDeterminedHashCellsAsTheirText()
    {
        AssertLikeEnumeration(rows => Texts(rows, _fixture.Digest));
    }

    [Fact]
    public void FiltersByDeterminedHashCellText()
    {
        AssertLikeEnumeration(rows => Ids(rows, _fixture.Digest, Text(_fixture.Digest)));
    }

    [Fact]
    public void NamesTheColumnAndItsTypeWhenItsCellsCannotBeRead()
    {
        IColumn id = new Column(new String("Id"), new LongColumnType());
        IColumn column = new Column(new String("Weird"), new CustomColumnType());
        ITable table = new Table(new String("Oddities"), [id, column], []);
        ISchema schema = new Schema(new String("Lab"), [table], []);
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        IQueryable<IRow> rows = new PostgreSqlStoredSchemaDataSet(schema, connection)[
            table
        ];

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            rows.Where(x => x.Cells[column].Value.TextValue != "").ToArray()
        );

        string messages = string.Join(" | ", Messages(exception));
        Assert.True(messages.Contains("'Weird'", StringComparison.Ordinal), messages);
        Assert.True(messages.Contains("'custom'", StringComparison.Ordinal), messages);
        Assert.Empty(connection.Statements);
    }

    private static IEnumerable<string> Messages(Exception? exception)
    {
        for (; exception != null; exception = exception.InnerException)
        {
            yield return exception.Message;
        }
    }

    private IColumn Column(string name)
    {
        return _fixture.Readings.Columns.Single(x => x.Name.TextValue == name);
    }

    private IQueryable<IRow> Table(IDbConnection connection)
    {
        return new PostgreSqlStoredSchemaDataSet(_fixture.Lab, connection)[
            _fixture.Readings
        ];
    }

    /// <summary>
    /// The text enumeration gives the cell of the column in the first row.
    /// </summary>
    private string Text(IColumn column)
    {
        return Table(_fixture.Connection)
            .AsEnumerable()
            .Single(x => x.Cells[_fixture.Id].Value.TextValue == "1")
            .Cells[column]
            .Value.TextValue;
    }

    private static string[] Texts(IQueryable<IRow> rows, IColumn column)
    {
        return
        [
            .. rows.Select(x => x.Cells[column].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal),
        ];
    }

    private string[] Ids(IQueryable<IRow> rows, IColumn column, string text)
    {
        return
        [
            .. rows.Where(x => x.Cells[column].Value.TextValue == text)
                .AsEnumerable()
                .Select(x => x.Cells[_fixture.Id].Value.TextValue)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The query returns over the stored table, in one statement, what it returns
    /// over the enumerated rows in memory.
    /// </summary>
    private void AssertLikeEnumeration(Func<IQueryable<IRow>, string[]> query)
    {
        string[] expected = query(
            new EnumerableQuery<IRow>([.. Table(_fixture.Connection).AsEnumerable()])
        );

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = query(Table(connection));

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
        _ = Assert.Single(connection.Statements);
    }

    private sealed record CustomColumnType : IColumnType
    {
        public IString Name { get; } = new String("custom");
    }
}
