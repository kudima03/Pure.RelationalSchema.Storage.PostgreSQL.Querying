using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of results that cannot be read yet are skipped until they can.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The <c>...By</c> operators return elements of their source, so their results must
/// be read as the elements of their source are, in one statement, and be what the
/// same query returns over the enumerated rows in memory.
/// </summary>
public sealed record ByOperatorElementTests
    : IClassFixture<QueryingFixture>,
        IDisposable
{
    private const string Pair = "System.ValueTuple`2[System.String,System.String]";

    private const string Row = "Pure.RelationalSchema.Storage.Abstractions.IRow";

    private const string DistinctByElement =
        "The element of DistinctBy is not read (#30), and the translator rejects "
        + "DistinctBy (kudima03/Linq.Expressions.Translation.PostgreSql#46).";

    private const string DistinctByText =
        "The translator rejects DistinctBy "
        + "(kudima03/Linq.Expressions.Translation.PostgreSql#46).";

    private const string ExceptByElement =
        "The element of ExceptBy is not read (#30), and the translator rejects ExceptBy.";

    private const string ExceptByText = "The translator rejects ExceptBy.";

    private const string IntersectByElement =
        "The element of IntersectBy is not read (#30), and the translator rejects "
        + "IntersectBy.";

    private const string IntersectByText = "The translator rejects IntersectBy.";

    private const string UnionByElement =
        "The element of UnionBy is not read (#30), and the translator rejects UnionBy.";

    private const string UnionByText = "The translator rejects UnionBy.";

    private const string MinByElement =
        "The element of MinBy is not read (#30), and the translator rejects MinBy.";

    private const string MinByText = "The translator rejects MinBy.";

    private const string MaxByElement =
        "The element of MaxBy is not read (#30), and the translator rejects MaxBy.";

    private const string MaxByText = "The translator rejects MaxBy.";

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

    [Fact(Skip = DistinctByElement)]
    public void ReadsDistinctByOverAOneElementValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            customers
                .Select(x => new ValueTuple<string>(x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Item1)
        );
    }

    [Fact(Skip = DistinctByElement)]
    public void ReadsDistinctByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers.OrderBy(x => x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Item2)
        );
    }

    [Fact(Skip = DistinctByElement)]
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

    [Fact(Skip = DistinctByElement)]
    public void ReadsDistinctByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers
                .OrderBy(x => x.Cells[Name].Value.TextValue)
                .DistinctBy(x => x.Cells[Active].Value.TextValue)
        );
    }

    [Fact(Skip = ExceptByElement)]
    public void ReadsExceptByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers).ExceptBy(Inactive(customers), x => x.Item1)
        );
    }

    [Fact(Skip = ExceptByElement)]
    public void ReadsExceptByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers.ExceptBy(Inactive(customers), x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact(Skip = IntersectByElement)]
    public void ReadsIntersectByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(customers).IntersectBy(Inactive(customers), x => x.Item1)
        );
    }

    [Fact(Skip = IntersectByElement)]
    public void ReadsIntersectByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            customers.IntersectBy(
                Inactive(customers),
                x => x.Cells[Name].Value.TextValue
            )
        );
    }

    [Fact(Skip = UnionByElement)]
    public void ReadsUnionByOverAValueTuple()
    {
        AssertSequenceLikeInMemory(customers =>
            Pairs(ActiveRows(customers))
                .UnionBy(Pairs(Bob(customers)), x => x.Item1)
        );
    }

    [Fact(Skip = UnionByElement)]
    public void ReadsUnionByOverRows()
    {
        AssertSequenceLikeInMemory(customers =>
            ActiveRows(customers)
                .UnionBy(Bob(customers), x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact(Skip = MinByElement)]
    public void ReadsMinByOverAValueTuple()
    {
        AssertElementLikeInMemory(customers =>
            Pairs(customers).MinBy(x => x.Item1)
        );
    }

    [Fact(Skip = MinByElement)]
    public void ReadsMinByOverRows()
    {
        AssertElementLikeInMemory(customers =>
            customers.MinBy(x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact(Skip = MaxByElement)]
    public void ReadsMaxByOverAValueTuple()
    {
        AssertElementLikeInMemory(customers =>
            Pairs(customers).MaxBy(x => x.Item1)
        );
    }

    [Fact(Skip = MaxByElement)]
    public void ReadsMaxByOverRows()
    {
        AssertElementLikeInMemory(customers =>
            customers.MaxBy(x => x.Cells[Name].Value.TextValue)
        );
    }

    [Fact(Skip = DistinctByText)]
    public void ReadsDistinctByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers.OrderBy(x => x.Cells[Name].Value.TextValue))
                .DistinctBy(x => x.Length)
        );
    }

    [Fact(Skip = ExceptByText)]
    public void ReadsExceptByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers).ExceptBy(Inactive(customers), x => x)
        );
    }

    [Fact(Skip = IntersectByText)]
    public void ReadsIntersectByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(customers).IntersectBy(Inactive(customers), x => x)
        );
    }

    [Fact(Skip = UnionByText)]
    public void ReadsUnionByOverText()
    {
        AssertSequenceLikeInMemory(customers =>
            Names(ActiveRows(customers)).UnionBy(Names(Bob(customers)), x => x)
        );
    }

    [Fact(Skip = MinByText)]
    public void ReadsMinByOverText()
    {
        AssertElementLikeInMemory(customers => Names(customers).MinBy(x => x));
    }

    [Fact(Skip = MaxByText)]
    public void ReadsMaxByOverText()
    {
        AssertElementLikeInMemory(customers => Names(customers).MaxBy(x => x));
    }

    [Fact]
    public void RefusesTheElementOfDistinctByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Pairs(Customers).DistinctBy(x => x.Item2).ToArray(),
            nameof(Queryable.DistinctBy),
            Pair
        );
    }

    [Fact]
    public void RefusesTheRowsOfDistinctByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Customers.DistinctBy(x => x.Cells[Active].Value.TextValue).ToArray(),
            nameof(Queryable.DistinctBy),
            Row
        );
    }

    [Fact]
    public void RefusesTheElementOfExceptByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Pairs(Customers).ExceptBy(Inactive(Customers), x => x.Item1).ToArray(),
            nameof(Queryable.ExceptBy),
            Pair
        );
    }

    [Fact]
    public void RefusesTheElementOfIntersectByBeforeRunningAStatement()
    {
        AssertRefused(
            () =>
                Pairs(Customers).IntersectBy(Inactive(Customers), x => x.Item1).ToArray(),
            nameof(Queryable.IntersectBy),
            Pair
        );
    }

    [Fact]
    public void RefusesTheElementOfUnionByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Pairs(Customers).UnionBy(Pairs(Bob(Customers)), x => x.Item1).ToArray(),
            nameof(Queryable.UnionBy),
            Pair
        );
    }

    [Fact]
    public void RefusesTheElementOfMinByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Pairs(Customers).MinBy(x => x.Item1),
            nameof(Queryable.MinBy),
            Pair
        );
    }

    [Fact]
    public void RefusesTheRowOfMaxByBeforeRunningAStatement()
    {
        AssertRefused(
            () => Customers.MaxBy(x => x.Cells[Name].Value.TextValue),
            nameof(Queryable.MaxBy),
            Row
        );
    }

    [Fact]
    public void TheTranslatorRejectsDistinctByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).DistinctBy(x => x.Length).ToArray(),
            nameof(Queryable.DistinctBy)
        );
    }

    [Fact]
    public void TheTranslatorRejectsExceptByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).ExceptBy(Inactive(Customers), x => x).ToArray(),
            nameof(Queryable.ExceptBy)
        );
    }

    [Fact]
    public void TheTranslatorRejectsIntersectByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).IntersectBy(Inactive(Customers), x => x).ToArray(),
            nameof(Queryable.IntersectBy)
        );
    }

    [Fact]
    public void TheTranslatorRejectsUnionByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).UnionBy(Names(Bob(Customers)), x => x).ToArray(),
            nameof(Queryable.UnionBy)
        );
    }

    [Fact]
    public void TheTranslatorRejectsMinByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).MinBy(x => x),
            nameof(Queryable.MinBy)
        );
    }

    [Fact]
    public void TheTranslatorRejectsMaxByOverText()
    {
        AssertRejectedByTheTranslator(
            () => Names(Customers).MaxBy(x => x),
            nameof(Queryable.MaxBy)
        );
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

    private void AssertRefused(Func<object?> query, string name, string type)
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(query);

        Assert.Equal(
            $"The query operator {name} returns {type}, which cannot be read from a "
                + "SQL result.",
            exception.Message
        );
        Assert.Empty(_connection.Statements);
    }

    private void AssertRejectedByTheTranslator(Func<object?> query, string name)
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(query);

        Assert.Equal(
            $"The query operator {name} cannot be translated to SQL.",
            exception.Message
        );
        Assert.Contains(
            $"'Queryable.{name}'",
            Assert.IsType<NotSupportedException>(exception.InnerException).Message,
            StringComparison.Ordinal
        );
        Assert.Empty(_connection.Statements);
    }

    private IQueryable<IRow> InMemory()
    {
        return new EnumerableQuery<IRow>(
            [
                .. new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
                    _fixture.Customers
                ].AsEnumerable(),
            ]
        );
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
