using System.Linq.Expressions;
using System.Text;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of one row of aggregates are skipped until such a root is translated.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Several aggregates of stored tables read as one row: a one-element constant root
/// whose selector holds scalar subqueries over the tables, which must run as one
/// statement with no FROM of its own.
/// </summary>
public sealed record OneRowAggregateTests : IClassFixture<QueryingFixture>, IDisposable
{
    private const string NotTranslated =
        "The one-element constant root of aggregates is refused as a source other "
        + "than a stored table, and the translator refuses it as an in-memory "
        + "source (#31).";

    private static readonly int[] One = [0];

    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    private readonly RecordingConnection _expected;

    public OneRowAggregateTests(QueryingFixture fixture)
    {
        _fixture = fixture;
        _connection = new RecordingConnection(fixture.Connection);
        _expected = new RecordingConnection(fixture.Connection);
    }

    private IQueryable<IRow> Customers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Customers];

    private IQueryable<IRow> Orders =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Orders];

    private IQueryable<IRow> Notes =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Crm, _connection)[_fixture.Notes];

    private IQueryable<IRow> ExpectedCustomers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _expected)[_fixture.Customers];

    private IQueryable<IRow> ExpectedOrders =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _expected)[_fixture.Orders];

    private IQueryable<IRow> ExpectedNotes =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Crm, _expected)[_fixture.Notes];

    [Fact(Skip = NotTranslated)]
    public void ReadsSeveralAggregatesOfATableAsOneRow()
    {
        IQueryable<IRow> orders = Orders;

        ValueTuple<long, int, string> row = Assert.Single(
            OneRow(_ => new ValueTuple<long, int, string>(
                orders.LongCount(),
                orders.Sum(x => x.Cells[_fixture.OrderTotal].Value.TextValue.Length),
                orders.Max(x => x.Cells[_fixture.OrderTotal].Value.TextValue)!
            ))
        );

        Assert.Equal(
            new ValueTuple<long, int, string>(
                ExpectedOrders.LongCount(),
                ExpectedOrders.Sum(x =>
                    x.Cells[_fixture.OrderTotal].Value.TextValue.Length
                ),
                ExpectedOrders.Max(x => x.Cells[_fixture.OrderTotal].Value.TextValue)!
            ),
            row
        );
        Assert.Equal(new ValueTuple<long, int, string>(3, 8, "40"), row);
        AssertOneStatementWithoutFrom("COUNT(", "SUM(", "MAX(");
    }

    [Fact(Skip = NotTranslated)]
    public void ReadsAggregatesOfTablesOfTwoSchemasAsOneRow()
    {
        IQueryable<IRow> customers = Customers;
        IQueryable<IRow> orders = Orders;
        IQueryable<IRow> notes = Notes;

        ValueTuple<long, int, long> row = Assert.Single(
            OneRow(_ => new ValueTuple<long, int, long>(
                customers.LongCount(),
                orders.Count(),
                notes.LongCount()
            ))
        );

        Assert.Equal(
            new ValueTuple<long, int, long>(
                ExpectedCustomers.LongCount(),
                ExpectedOrders.Count(),
                ExpectedNotes.LongCount()
            ),
            row
        );
        Assert.Equal(new ValueTuple<long, int, long>(3, 3, 2), row);
        AssertOneStatementWithoutFrom("COUNT(");
    }

    [Fact(Skip = NotTranslated)]
    public void ReadsFilteredAggregatesAndQuantifiersAsOneRow()
    {
        IQueryable<IRow> customers = Customers;
        IQueryable<IRow> orders = Orders;

        ValueTuple<int, bool, bool, string> row = Assert.Single(
            OneRow(_ => new ValueTuple<int, bool, bool, string>(
                orders.Count(x => x.Cells[_fixture.OrderCustomer].Value.TextValue == "1"),
                customers.Any(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue == "Bob"
                ),
                orders.All(x => x.Cells[_fixture.OrderPriority].Value.TextValue == "1"),
                customers
                    .Where(x =>
                        x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
                    )
                    .Min(x => x.Cells[_fixture.CustomerName].Value.TextValue)!
            ))
        );

        Assert.Equal(
            new ValueTuple<int, bool, bool, string>(
                ExpectedOrders.Count(x =>
                    x.Cells[_fixture.OrderCustomer].Value.TextValue == "1"
                ),
                ExpectedCustomers.Any(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue == "Bob"
                ),
                ExpectedOrders.All(x =>
                    x.Cells[_fixture.OrderPriority].Value.TextValue == "1"
                ),
                ExpectedCustomers
                    .Where(x =>
                        x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
                    )
                    .Min(x => x.Cells[_fixture.CustomerName].Value.TextValue)!
            ),
            row
        );
        Assert.Equal(new ValueTuple<int, bool, bool, string>(2, true, false, "Ann"), row);
        AssertOneStatementWithoutFrom("COUNT(", "MIN(");
    }

    [Fact(Skip = NotTranslated)]
    public void ReadsTheOneRowOfAggregatesAsASingleElement()
    {
        IQueryable<IRow> customers = Customers;
        IQueryable<IRow> notes = Notes;

        ValueTuple<long, long> row = OneRow(_ => new ValueTuple<long, long>(
                customers.LongCount(),
                notes.LongCount()
            ))
            .Single();

        Assert.Equal(
            new ValueTuple<long, long>(
                ExpectedCustomers.LongCount(),
                ExpectedNotes.LongCount()
            ),
            row
        );
        AssertOneStatementWithoutFrom("COUNT(");
    }

    [Fact]
    public void ThrowsWhenTheOneRowReadsTablesOfDifferentConnections()
    {
        using RecordingConnection other = new(_fixture.Connection);
        IQueryable<IRow> orders = Orders;
        IQueryable<IRow> customers = new PostgreSqlStoredSchemaDataSet(
            _fixture.Shop,
            other
        )[_fixture.Customers];

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            OneRow(_ => new ValueTuple<long, long>(
                    orders.LongCount(),
                    customers.LongCount()
                ))
                .ToArray()
        );

        Assert.Equal(
            "The query operator Select reads sources other than stored tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(_connection.Statements);
        Assert.Empty(other.Statements);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _expected.Dispose();
    }

    /// <summary>
    /// The text of the SQL outside parentheses, which is that of the statement
    /// itself and not of its subqueries.
    /// </summary>
    private static string TopLevel(string sql)
    {
        StringBuilder text = new();
        int depth = 0;
        foreach (char character in sql)
        {
            depth += character switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth == 0 && character != ')')
            {
                _ = text.Append(character);
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// The one-element constant root with the selector, as a query of the provider
    /// of the stored tables.
    /// </summary>
    private IQueryable<T> OneRow<T>(Expression<Func<int, T>> selector)
    {
        return Orders.Provider.CreateQuery<T>(
            One.AsQueryable().Select(selector).Expression
        );
    }

    private void AssertOneStatementWithoutFrom(params string[] aggregates)
    {
        string statement = Assert.Single(_connection.Statements);
        Assert.StartsWith("SELECT (", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM", TopLevel(statement), StringComparison.Ordinal);
        Assert.All(aggregates, x => Assert.Contains(x, statement));
    }
}
