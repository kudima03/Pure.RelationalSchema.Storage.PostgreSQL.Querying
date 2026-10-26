using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record QueryOperatorTranslationTests
    : IClassFixture<QueryingFixture>,
        IDisposable
{
    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    public QueryOperatorTranslationTests(QueryingFixture fixture)
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
    public void FiltersByCombinedConditions()
    {
        string[] names =
        [
            .. Customers
                .Where(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue == "Bob"
                    || (x.Cells[_fixture.CustomerName].Value.TextValue != "Ann"
                        && x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                )
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Order(),
        ];

        Assert.Equal(["Bob", "Cid"], names);
        Assert.Contains(" OR ", Statement);
    }

    [Fact]
    public void FiltersByNegatedCondition()
    {
        IRow row = Assert.Single(
            Customers.Where(x =>
                !(x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
            )
        );

        Assert.Equal("Bob", row.Cells[_fixture.CustomerName].Value.TextValue);
        Assert.Contains(" WHERE ", Statement);
    }

    [Fact]
    public void FiltersByChainedConditions()
    {
        IRow row = Assert.Single(
            Customers
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                .Where(x => x.Cells[_fixture.CustomerName].Value.TextValue != "Ann")
        );

        Assert.Equal("3", row.Cells[_fixture.CustomerId].Value.TextValue);
        Assert.Contains(" WHERE ", Statement);
    }

    [Fact]
    public void FiltersByCapturedValues()
    {
        string name = "Cid";

        IRow row = Assert.Single(
            Customers.Where(x => x.Cells[_fixture.CustomerName].Value.TextValue == name)
        );

        Assert.Equal("3", row.Cells[_fixture.CustomerId].Value.TextValue);
        Assert.Contains("'Cid'", Statement);
    }

    [Fact]
    public void FiltersByMembershipInAnInMemoryCollection()
    {
        string[] names = ["Ann", "Cid", "Zed"];

        string[] ids =
        [
            .. Customers
                .Where(x =>
                    names.Contains(x.Cells[_fixture.CustomerName].Value.TextValue)
                )
                .Select(x => x.Cells[_fixture.CustomerId].Value.TextValue)
                .Order(),
        ];

        Assert.Equal(["1", "3"], ids);
        Assert.Contains(" IN ", Statement);
    }

    [Fact]
    public void FiltersByTextPatterns()
    {
        Assert.Equal(
            [1, 1, 1, 0],
            [
                Customers.Count(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue.StartsWith('A')
                ),
                Customers.Count(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue.EndsWith('b')
                ),
                Customers.Count(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue.Contains('i')
                ),
                Customers.Count(x =>
                    x.Cells[_fixture.CustomerName].Value.TextValue.Contains('%')
                ),
            ]
        );
        Assert.Equal(4, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains(" LIKE ", x));
    }

    [Fact]
    public void FiltersByTextLength()
    {
        Assert.Equal(
            0,
            Customers.Count(x =>
                x.Cells[_fixture.CustomerName].Value.TextValue.Length > 3
                || string.IsNullOrEmpty(x.Cells[_fixture.CustomerName].Value.TextValue)
            )
        );
        Assert.Contains("COUNT(", Statement);
    }

    [Fact]
    public void EscapesTextLiterals()
    {
        Assert.Empty(
            Customers.Where(x =>
                x.Cells[_fixture.CustomerName].Value.TextValue == "O'Brien"
            )
        );
        Assert.Contains("'O''Brien'", Statement);
    }

    [Fact]
    public void ProjectsTextFunctions()
    {
        string[] names =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerId].Value.TextValue == "1")
                .Select(x => new
                {
                    Upper = x.Cells[_fixture.CustomerName]
                        .Value.TextValue.ToUpperInvariant(),
                    Lower = x.Cells[_fixture.CustomerName]
                        .Value.TextValue.ToLowerInvariant(),
                    First = x.Cells[_fixture.CustomerName]
                        .Value.TextValue.Substring(0, 1),
                    Replaced = x.Cells[_fixture.CustomerName]
                        .Value.TextValue.Replace("n", "m"),
                    Trimmed = (" " + x.Cells[_fixture.CustomerName].Value.TextValue + " ")
                        .Trim(),
                    Index = x.Cells[_fixture.CustomerName].Value.TextValue.IndexOf('n'),
                })
                .AsEnumerable()
                .Select(x =>
                    $"{x.Upper}|{x.Lower}|{x.First}|{x.Replaced}|{x.Trimmed}|{x.Index}"
                ),
        ];

        Assert.Equal(["ANN|ann|A|Amm|Ann|1"], names);
        _ = Statement;
    }

    [Fact]
    public void ProjectsConcatenatedAndInterpolatedText()
    {
        string[] names =
        [
            .. Customers
                .Select(x =>
                    $"{x.Cells[_fixture.CustomerName].Value.TextValue}#"
                    + x.Cells[_fixture.CustomerId].Value.TextValue
                )
                .Order(),
        ];

        Assert.Equal(["Ann#1", "Bob#2", "Cid#3"], names);
        _ = Statement;
    }

    [Fact]
    public void ProjectsConditionals()
    {
        string[] names =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerId].Value.TextValue)
                .Select(x =>
                    x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
                        ? x.Cells[_fixture.CustomerName].Value.TextValue
                        : "-"
                ),
        ];

        Assert.Equal(["Ann", "-", "Cid"], names);
        Assert.Contains(" CASE ", Statement);
    }

    [Fact]
    public void ProjectsMathFunctions()
    {
        int[] values =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerId].Value.TextValue)
                .Select(x =>
                    Math.Max(x.Cells[_fixture.CustomerId].Value.TextValue.Length, 2)
                    + Math.Abs(-x.Cells[_fixture.CustomerName].Value.TextValue.Length)
                ),
        ];

        Assert.Equal([5, 5, 5], values);
        _ = Statement;
    }

    [Fact]
    public void ProjectsNestedAnonymousTypes()
    {
        var customer = Customers
            .Where(x => x.Cells[_fixture.CustomerId].Value.TextValue == "2")
            .Select(x => new
            {
                Name = x.Cells[_fixture.CustomerName].Value.TextValue,
                Details = new
                {
                    Active = x.Cells[_fixture.CustomerActive].Value.TextValue == "True",
                    Length = (long)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                },
            })
            .Single();

        Assert.Equal("Bob", customer.Name);
        Assert.False(customer.Details.Active);
        Assert.Equal(3L, customer.Details.Length);
        _ = Statement;
    }

    [Fact]
    public void ProjectsDistinctValues()
    {
        string[] values =
        [
            .. Customers
                .Select(x => x.Cells[_fixture.CustomerActive].Value.TextValue)
                .Distinct()
                .Order(),
        ];

        Assert.Equal(["False", "True"], values);
        Assert.Contains("DISTINCT", Statement);
    }

    [Fact]
    public void OrdersByThenBy()
    {
        string[] orders =
        [
            .. Orders
                .OrderBy(x => x.Cells[_fixture.OrderCustomer].Value.TextValue)
                .ThenByDescending(x => x.Cells[_fixture.OrderShelf].Value.TextValue)
                .Select(x =>
                    x.Cells[_fixture.OrderCustomer].Value.TextValue
                    + ":"
                    + x.Cells[_fixture.OrderShelf].Value.TextValue
                ),
        ];

        Assert.Equal(["1:20", "1:10", "3:300"], orders);
        Assert.Contains(" DESC", Statement);
    }

    [Fact]
    public void OrdersDescendingByValue()
    {
        string[] names =
        [
            .. Customers
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .OrderDescending(),
        ];

        Assert.Equal(["Cid", "Bob", "Ann"], names);
        Assert.Contains(" ORDER BY ", Statement);
    }

    [Fact]
    public void PagesOrderedRows()
    {
        string[] names =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Skip(1)
                .Take(1)
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue),
        ];

        Assert.Equal(["Bob"], names);
        Assert.Contains(" LIMIT ", Statement);
    }

    [Fact]
    public void FiltersAfterPaging()
    {
        string[] names =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Take(2)
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue),
        ];

        Assert.Equal(["Ann"], names);
        Assert.Contains(" LIMIT ", Statement);
    }

    [Fact]
    public void TakesAndSkipsTheLastRows()
    {
        IQueryable<string> names = Customers
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
            .Order();

        Assert.Equal(["Bob", "Cid"], names.TakeLast(2));
        Assert.Equal(["Ann"], names.SkipLast(2));
        Assert.Equal(2, _connection.Statements.Count);
    }

    [Fact]
    public void ReversesOrderedRows()
    {
        string[] names =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Reverse()
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue),
        ];

        Assert.Equal(["Cid", "Bob", "Ann"], names);
        _ = Statement;
    }

    [Fact]
    public void CountsMatchingRows()
    {
        Assert.Equal(
            2,
            Customers.Count(x =>
                x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
            )
        );
        Assert.Equal(
            1L,
            Customers.LongCount(x =>
                x.Cells[_fixture.CustomerActive].Value.TextValue == "False"
            )
        );
        Assert.Equal(2, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains("COUNT(", x));
    }

    [Fact]
    public void AveragesInSql()
    {
        Assert.Equal(
            3.0,
            Customers.Average(x => x.Cells[_fixture.CustomerName].Value.TextValue.Length)
        );
        Assert.Contains("AVG(", Statement);
    }

    [Fact]
    public void AggregatesTextAndCountsDistinctValues()
    {
        Assert.Equal(
            "Cid",
            Customers.Max(x => x.Cells[_fixture.CustomerName].Value.TextValue)
        );
        Assert.Equal(
            2,
            Customers
                .Select(x => x.Cells[_fixture.CustomerActive].Value.TextValue)
                .Distinct()
                .Count()
        );
        Assert.Equal(2, _connection.Statements.Count);
    }

    [Fact]
    public void AnswersQuantifiersOverNoRows()
    {
        IQueryable<IRow> none = Customers.Where(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue == "Zed"
        );

        Assert.False(none.Any());
        Assert.True(none.All(x => x.Cells[_fixture.CustomerName].Value.TextValue == ""));
        Assert.False(
            Customers
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Contains("Zed")
        );
        Assert.Equal(3, _connection.Statements.Count);
    }

    [Fact]
    public void AggregatesNoRowsWithDefaults()
    {
        Assert.Equal(
            0,
            Customers
                .Where(x => x.Cells[_fixture.CustomerName].Value.TextValue == "Zed")
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue.Length)
                .DefaultIfEmpty()
                .Max()
        );
        _ = Statement;
    }

    [Fact]
    public void ReturnsElementsMatchingPredicates()
    {
        IQueryable<IRow> ordered = Customers.OrderBy(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue
        );

        Assert.Equal(
            ["Bob", "Cid", "Cid"],
            [
                ordered
                    .First(x => x.Cells[_fixture.CustomerId].Value.TextValue != "1")
                    .Cells[_fixture.CustomerName]
                    .Value.TextValue,
                ordered
                    .Last(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                    .Cells[_fixture.CustomerName]
                    .Value.TextValue,
                ordered
                    .SingleOrDefault(x =>
                        x.Cells[_fixture.CustomerId].Value.TextValue == "3"
                    )!
                    .Cells[_fixture.CustomerName]
                    .Value.TextValue,
            ]
        );
        Assert.Null(
            ordered.FirstOrDefault(x =>
                x.Cells[_fixture.CustomerName].Value.TextValue == "Zed"
            )
        );
        Assert.Equal(4, _connection.Statements.Count);
    }

    [Fact]
    public void ReturnsProjectedElements()
    {
        IQueryable<string> names = Customers
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
            .Order();

        Assert.Equal(
            ["Ann", "Cid", "Bob"],
            [names.First(), names.Last(), names.ElementAt(1)]
        );
        Assert.Null(names.Where(x => x == "Zed").FirstOrDefault());
        Assert.Equal(4, _connection.Statements.Count);
    }

    [Fact]
    public void ConcatenatesRowsOfOneTable()
    {
        Assert.Equal(
            6,
            Customers.Concat(Customers).Count()
        );
        Assert.Contains(" UNION ALL ", Statement);
    }

    [Fact]
    public void IntersectsAndExceptsProjectedValues()
    {
        IQueryable<string> active = Customers
            .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue);
        IQueryable<string> notAnn = Customers
            .Where(x => x.Cells[_fixture.CustomerName].Value.TextValue != "Ann")
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue);

        Assert.Equal(["Cid"], active.Intersect(notAnn));
        Assert.Equal(["Ann"], active.Except(notAnn));
        Assert.Equal(2, _connection.Statements.Count);
        Assert.Contains(_connection.Statements, x => x.Contains(" INTERSECT "));
        Assert.Contains(_connection.Statements, x => x.Contains(" EXCEPT "));
    }

    [Fact]
    public void UnitesValuesOfTablesOfOneSchema()
    {
        string[] ids =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "False")
                .Select(x => x.Cells[_fixture.CustomerId].Value.TextValue)
                .Union(
                    Orders.Select(x => x.Cells[_fixture.OrderCustomer].Value.TextValue)
                )
                .Order(),
        ];

        Assert.Equal(["1", "2", "3"], ids);
        Assert.Contains(" UNION ", Statement);
    }

    [Fact]
    public void ReadsRowsOfEveryTranslatedColumnType()
    {
        IRow translated = Assert.Single(
            Orders.Where(x => x.Cells[_fixture.OrderUnits].Value.TextValue == "1234")
        );

        IRow read = Orders
            .AsEnumerable()
            .Single(x => x.Cells[_fixture.OrderUnits].Value.TextValue == "1234");

        Assert.Equal(
            _fixture.Orders.Columns.Select(x => read.Cells[x].Value.TextValue),
            _fixture.Orders.Columns.Select(x => translated.Cells[x].Value.TextValue)
        );
        Assert.Equal(2, _connection.Statements.Count);
    }

    [Fact]
    public void FiltersByTextOfEveryTranslatedColumnType()
    {
        IColumn[] columns =
        [
            _fixture.OrderCustomer,
            _fixture.OrderTotal,
            _fixture.OrderCode,
            _fixture.OrderUnits,
            _fixture.OrderPriority,
            _fixture.OrderShelf,
        ];

        string[] values =
        [
            .. columns.Select(column =>
                Orders
                    .Where(x => x.Cells[_fixture.OrderTotal].Value.TextValue == "40")
                    .Select(x => x.Cells[column].Value.TextValue)
                    .Single()
            ),
        ];

        Assert.Equal(
            ["3", "40", "00000000-0000-0000-0000-00000000000c", "1234", "1", "300"],
            values
        );
        Assert.Equal(6, _connection.Statements.Count);
    }

    [Fact]
    public void OrdersByTextOfUuidCells()
    {
        string[] totals =
        [
            .. Orders
                .Where(x =>
                    x.Cells[_fixture.OrderCode].Value.TextValue.StartsWith('0')
                )
                .OrderByDescending(x => x.Cells[_fixture.OrderCode].Value.TextValue)
                .Select(x => x.Cells[_fixture.OrderTotal].Value.TextValue),
        ];

        Assert.Equal(["40", "250", "100"], totals);
        _ = Statement;
    }

    [Fact]
    public void ReadsScalarsOfEveryReadableType()
    {
        var values = Customers
            .Where(x => x.Cells[_fixture.CustomerId].Value.TextValue == "1")
            .Select(x => new
            {
                Short = (short)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                Long = (long)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                Float = (float)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                Double = (double)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                Decimal = (decimal)x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                Bool = x.Cells[_fixture.CustomerActive].Value.TextValue == "True",
                Nullable = (int?)null,
            })
            .Single();

        Assert.Equal((short)3, values.Short);
        Assert.Equal(3L, values.Long);
        Assert.Equal(3f, values.Float);
        Assert.Equal(3d, values.Double);
        Assert.Equal(3m, values.Decimal);
        Assert.True(values.Bool);
        Assert.Null(values.Nullable);
        _ = Statement;
    }

    [Fact]
    public void ReadsNestedValueTuples()
    {
        ValueTuple<string, ValueTuple<int, string>>[] customers =
        [
            .. Customers
                .OrderBy(x => x.Cells[_fixture.CustomerId].Value.TextValue)
                .Select(x => new ValueTuple<string, ValueTuple<int, string>>(
                    x.Cells[_fixture.CustomerName].Value.TextValue,
                    new ValueTuple<int, string>(
                        x.Cells[_fixture.CustomerName].Value.TextValue.Length,
                        x.Cells[_fixture.CustomerId].Value.TextValue
                    )
                )),
        ];

        Assert.Equal(
            [("Ann", (3, "1")), ("Bob", (3, "2")), ("Cid", (3, "3"))],
            customers
        );
        _ = Statement;
    }

    [Fact]
    public void ReadsRowsOfSetOperationsOfOneTable()
    {
        IQueryable<IRow> bob = Customers.Where(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue == "Bob"
        );

        string[] except =
        [
            .. Customers
                .Except(bob)
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Order(),
        ];
        IRow intersected = Assert.Single(Customers.Intersect(bob));

        Assert.Equal(["Ann", "Cid"], except);
        Assert.Equal("2", intersected.Cells[_fixture.CustomerId].Value.TextValue);
        Assert.Equal(2, _connection.Statements.Count);
    }

    [Fact]
    public void ReadsRowsThroughRowPreservingOperators()
    {
        IRow row = Customers
            .Distinct()
            .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
            .OrderByDescending(x => x.Cells[_fixture.CustomerName].Value.TextValue)
            .ThenBy(x => x.Cells[_fixture.CustomerId].Value.TextValue)
            .Skip(0)
            .Take(2)
            .Reverse()
            .TakeLast(1)
            .Single();

        Assert.Equal("Cid", row.Cells[_fixture.CustomerName].Value.TextValue);
        Assert.Equal("3", row.Cells[_fixture.CustomerId].Value.TextValue);
        _ = Statement;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
