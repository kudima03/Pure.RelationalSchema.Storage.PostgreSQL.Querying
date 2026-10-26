using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record QueryCompositionTranslationTests
    : IClassFixture<QueryingFixture>,
        IDisposable
{
    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    public QueryCompositionTranslationTests(QueryingFixture fixture)
    {
        _fixture = fixture;
        _connection = new RecordingConnection(fixture.Connection);
    }

    private IQueryable<IRow> Customers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Customers];

    private IQueryable<IRow> Orders =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Orders];

    private string Statement => Assert.Single(_connection.Statements);

    [Fact]
    public void FiltersAndOrdersJoinedRows()
    {
        string[] totals =
        [
            .. Customers
                .Join(
                    Orders,
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    x => x.Cells[_fixture.OrderCustomer].Value.TextValue,
                    (customer, order) =>
                        new
                        {
                            Name = customer.Cells[_fixture.CustomerName].Value.TextValue,
                            Shelf = order.Cells[_fixture.OrderShelf].Value.TextValue,
                        }
                )
                .Where(x => x.Name == "Ann")
                .OrderByDescending(x => x.Shelf)
                .Select(x => x.Shelf),
        ];

        Assert.Equal(["20", "10"], totals);
        Assert.Contains(" JOIN ", Statement);
    }

    [Fact]
    public void JoinsATableWithItself()
    {
        Assert.Equal(
            3,
            Customers
                .Join(
                    Customers,
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    (left, right) => right.Cells[_fixture.CustomerName].Value.TextValue
                )
                .Count()
        );
        Assert.Contains(" JOIN ", Statement);
    }

    [Fact]
    public void CountsGroupsOfJoinedRows()
    {
        string[] counts =
        [
            .. Customers
                .GroupJoin(
                    Orders,
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    x => x.Cells[_fixture.OrderCustomer].Value.TextValue,
                    (customer, orders) =>
                        customer.Cells[_fixture.CustomerName].Value.TextValue
                        + ":"
                        + orders.Count().ToString()
                )
                .Order(),
        ];

        Assert.Equal(["Ann:2", "Bob:0", "Cid:1"], counts);
        _ = Statement;
    }

    [Fact]
    public void LeftJoinsRows()
    {
        string[] totals =
        [
            .. Customers
                .GroupJoin(
                    Orders,
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    x => x.Cells[_fixture.OrderCustomer].Value.TextValue,
                    (customer, orders) => new { Customer = customer, Orders = orders }
                )
                .SelectMany(
                    x => x.Orders.DefaultIfEmpty(),
                    (x, order) =>
                        x.Customer.Cells[_fixture.CustomerName].Value.TextValue
                        + ":"
                        + (
                            order == null
                                ? "-"
                                : order.Cells[_fixture.OrderTotal].Value.TextValue
                        )
                )
                .Order(),
        ];

        Assert.Equal(["Ann:100", "Ann:250", "Bob:-", "Cid:40"], totals);
        Assert.Contains(" LEFT JOIN ", Statement);
    }

    [Fact]
    public void CrossJoinsRows()
    {
        Assert.Equal(
            9,
            Customers
                .SelectMany(
                    x => Orders,
                    (customer, order) => order.Cells[_fixture.OrderTotal].Value.TextValue
                )
                .Count()
        );
        _ = Statement;
    }

    [Fact]
    public void JoinsCorrelatedRows()
    {
        IQueryable<IRow> orders = Orders;

        string[] totals =
        [
            .. Customers
                .SelectMany(
                    customer =>
                        orders.Where(order =>
                            order.Cells[_fixture.OrderCustomer].Value.TextValue
                            == customer.Cells[_fixture.CustomerId].Value.TextValue
                        ),
                    (customer, order) =>
                        customer.Cells[_fixture.CustomerName].Value.TextValue
                        + ":"
                        + order.Cells[_fixture.OrderPriority].Value.TextValue
                )
                .Order(),
        ];

        Assert.Equal(["Ann:1", "Ann:2", "Cid:1"], totals);
        _ = Statement;
    }

    [Fact]
    public void GroupsRowsByCellText()
    {
        string[] groups =
        [
            .. Orders
                .GroupBy(x => x.Cells[_fixture.OrderCustomer].Value.TextValue)
                .Select(x => x.Key + ":" + x.Count().ToString())
                .Order(),
        ];

        Assert.Equal(["1:2", "3:1"], groups);
        Assert.Contains(" GROUP BY ", Statement);
    }

    [Fact]
    public void FiltersGroups()
    {
        string[] customers =
        [
            .. Orders
                .GroupBy(x => x.Cells[_fixture.OrderCustomer].Value.TextValue)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key),
        ];

        Assert.Equal(["1"], customers);
        Assert.Contains(" HAVING ", Statement);
    }

    [Fact]
    public void AggregatesGroups()
    {
        var groups = Orders
            .GroupBy(
                x => x.Cells[_fixture.OrderCustomer].Value.TextValue,
                x => x.Cells[_fixture.OrderTotal].Value.TextValue
            )
            .Select(x => new
            {
                Customer = x.Key,
                Max = x.Max(),
                Length = x.Sum(total => total.Length),
                Large = x.Count(total => total.Length > 2),
            })
            .OrderBy(x => x.Customer)
            .ToArray();

        Assert.Equal(
            ["1:250:6:2", "3:40:2:0"],
            groups.Select(x => $"{x.Customer}:{x.Max}:{x.Length}:{x.Large}")
        );
        Assert.Contains(" FILTER ", Statement);
    }

    [Fact]
    public void GroupsRowsByCompositeKeys()
    {
        string[] groups =
        [
            .. Orders
                .GroupBy(x => new
                {
                    Customer = x.Cells[_fixture.OrderCustomer].Value.TextValue,
                    Priority = x.Cells[_fixture.OrderPriority].Value.TextValue,
                })
                .Select(x => x.Key.Customer + ":" + x.Key.Priority)
                .Order(),
        ];

        Assert.Equal(["1:1", "1:2", "3:1"], groups);
        Assert.Contains(" GROUP BY ", Statement);
    }

    [Fact]
    public void AnswersQuantifiersOverGroups()
    {
        string[] customers =
        [
            .. Customers
                .GroupBy(x => x.Cells[_fixture.CustomerActive].Value.TextValue)
                .Where(x =>
                    x.All(customer =>
                        customer.Cells[_fixture.CustomerName].Value.TextValue != "Bob"
                    )
                )
                .Select(x => x.Key),
        ];

        Assert.Equal(["True"], customers);
        _ = Statement;
    }

    [Fact]
    public void ProjectsScalarSubqueries()
    {
        IQueryable<IRow> orders = Orders;

        string[] counts =
        [
            .. Customers
                .Select(customer => new
                {
                    Name = customer.Cells[_fixture.CustomerName].Value.TextValue,
                    Orders = orders.Count(order =>
                        order.Cells[_fixture.OrderCustomer].Value.TextValue
                        == customer.Cells[_fixture.CustomerId].Value.TextValue
                    ),
                })
                .AsEnumerable()
                .Select(x => $"{x.Name}:{x.Orders}")
                .Order(),
        ];

        Assert.Equal(["Ann:2", "Bob:0", "Cid:1"], counts);
        _ = Statement;
    }

    [Fact]
    public void FiltersByMembershipInASubquery()
    {
        IQueryable<string> buyers = Orders.Select(x =>
            x.Cells[_fixture.OrderCustomer].Value.TextValue
        );

        string[] names =
        [
            .. Customers
                .Where(x =>
                    !buyers.Contains(x.Cells[_fixture.CustomerId].Value.TextValue)
                )
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue),
        ];

        Assert.Equal(["Bob"], names);
        Assert.Contains(" IN ", Statement);
    }

    [Fact]
    public void FiltersByCorrelatedQuantifiers()
    {
        IQueryable<IRow> orders = Orders;

        string[] names =
        [
            .. Customers
                .Where(customer =>
                    orders
                        .Where(order =>
                            order.Cells[_fixture.OrderCustomer].Value.TextValue
                            == customer.Cells[_fixture.CustomerId].Value.TextValue
                        )
                        .All(order =>
                            order.Cells[_fixture.OrderPriority].Value.TextValue == "1"
                        )
                )
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Order(),
        ];

        Assert.Equal(["Bob", "Cid"], names);
        _ = Statement;
    }

    [Fact]
    public void FiltersByUncorrelatedScalarSubqueries()
    {
        IQueryable<IRow> orders = Orders;

        string[] totals =
        [
            .. orders
                .Where(x =>
                    x.Cells[_fixture.OrderTotal].Value.TextValue
                    == orders.Max(order =>
                        order.Cells[_fixture.OrderTotal].Value.TextValue
                    )
                )
                .Select(x => x.Cells[_fixture.OrderShelf].Value.TextValue),
        ];

        Assert.Equal(["300"], totals);
        _ = Statement;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
