using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The <c>...By</c> operators return elements of their source, so their results must
/// be read as the elements of their source are, in one statement, and be what the
/// same query returns over the enumerated rows in memory.
/// </summary>
public sealed record ByOperatorElementTests : IClassFixture<QueryingFixture>, IDisposable
{
    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    public ByOperatorElementTests(QueryingFixture fixture)
    {
        _fixture = fixture;
        _connection = new RecordingConnection(fixture.Connection);
    }

    private IColumn Name => _fixture.CustomerName;

    private IColumn Active => _fixture.CustomerActive;

    private IQueryable<IRow> Customers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Customers];

    [Fact]
    public void ReadsDistinctByOverAOneElementValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            customers
                .Select(x => new ValueTuple<string>(x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Item1)
        );
    }

    [Fact]
    public void ReadsDistinctByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers.OrderBy(x => x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Item2)
        );
    }

    [Fact]
    public void ReadsDistinctByOverAnAnonymousType()
    {
        AssertSequenceLikeInMemory(customers =>
            customers
                .OrderBy(x => x.Cells[Name].Value.TextValue)
                .Select(x => new
                {
                    Name = x.Cells[Name].Value.TextValue,
                    Active = x.Cells[Active].Value.TextValue,
                })
                .DistinctBy(x => x.Active)
        );
    }

    [Fact]
    public void ReadsDistinctByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers
                .OrderBy(x => x.Cells[Name].Value.TextValue)
                .DistinctBy(x => x.Cells[Active].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsExceptByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers).ExceptBy(Inactive(customers), x => x.Item1)
        );
    }

    [Fact]
    public void ReadsExceptByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers.ExceptBy(Inactive(customers), x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsIntersectByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers).IntersectBy(Inactive(customers), x => x.Item1)
        );
    }

    [Fact]
    public void ReadsIntersectByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers.IntersectBy(Inactive(customers), x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsUnionByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(ActiveRows(customers)).UnionBy(Pairs(Bob(customers)), x => x.Item1)
        );
    }

    [Fact]
    public void ReadsUnionByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            ActiveRows(customers)
                .UnionBy(Bob(customers), x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsMinByOverAValueTuple()
    {
        AssertElementLikeInMemory(customers => Pairs(customers).MinBy(x => x.Item1));
    }

    [Fact]
    public void ReadsMinByOverRows()
    {
        AssertElementLikeInMemory(customers =>
            customers.MinBy(x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsMaxByOverAValueTuple()
    {
        AssertElementLikeInMemory(customers => Pairs(customers).MaxBy(x => x.Item1));
    }

    [Fact]
    public void ReadsMaxByOverRows()
    {
        AssertElementLikeInMemory(customers =>
            customers.MaxBy(x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsDistinctByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers.OrderBy(x => x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Length)
        );
    }

    [Fact]
    public void ReadsExceptByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers).ExceptBy(Inactive(customers), x => x)
        );
    }

    [Fact]
    public void ReadsIntersectByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers).IntersectBy(Inactive(customers), x => x)
        );
    }

    [Fact]
    public void ReadsUnionByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(ActiveRows(customers)).UnionBy(Names(Bob(customers)), x => x)
        );
    }

    [Fact]
    public void ReadsMinByOverText()
    {
        AssertElementLikeInMemory(customers => Names(customers).MinBy(x => x));
    }

    [Fact]
    public void ReadsMaxByOverText()
    {
        AssertElementLikeInMemory(customers => Names(customers).MaxBy(x => x));
    }

    private IQueryable<(string, string)> Pairs(IQueryable<IRow> customers)
    {
        return customers.Select(x => new ValueTuple<string, string>(
            x.Cells[Name].Value.TextValue,
            x.Cells[Active].Value.TextValue
        ));
    }

    private IQueryable<string> Names(IQueryable<IRow> customers)
    {
        return customers.Select(x => x.Cells[Name].Value.TextValue);
    }

    private IQueryable<string> Inactive(IQueryable<IRow> customers)
    {
        return Names(customers.Where(x => x.Cells[Active].Value.TextValue == "False"));
    }

    private IQueryable<IRow> ActiveRows(IQueryable<IRow> customers)
    {
        return customers.Where(x => x.Cells[Active].Value.TextValue == "True");
    }

    private IQueryable<IRow> Bob(IQueryable<IRow> customers)
    {
        return customers.Where(x => x.Cells[Name].Value.TextValue == "Bob");
    }

    private void AssertSequenceLikeInMemory<T>(
        Func<IQueryable<IRow>, IQueryable<T>> query
    )
    {
        string[] expected = Texts(query(InMemory()));

        string[] actual = Texts(query(Customers));

        Assert.Equal(expected, actual);
        _ = Assert.Single(_connection.Statements);
    }

    private void AssertElementLikeInMemory<T>(Func<IQueryable<IRow>, T> query)
    {
        string? expected = Text(query(InMemory()));

        string? actual = Text(query(Customers));

        Assert.Equal(expected, actual);
        _ = Assert.Single(_connection.Statements);
    }

    private IQueryable<IRow> InMemory()
    {
        return new EnumerableQuery<IRow>([
            .. new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
                _fixture.Customers
            ]
                .AsEnumerable(),
        ]);
    }

    private string[] Texts<T>(IEnumerable<T> values)
    {
        return [.. values.Select(x => Text(x)!).Order(StringComparer.Ordinal)];
    }

    private string? Text<T>(T value)
    {
        return value is IRow row
            ? string.Join(
                ", ",
                _fixture.Customers.Columns.Select(x => row.Cells[x].Value.TextValue)
            )
            : value?.ToString();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
