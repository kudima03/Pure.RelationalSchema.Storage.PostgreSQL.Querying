using System.Collections;
using System.Linq.Expressions;
using Pure.Primitives.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of queries that ran in memory are skipped until they are translated.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

public sealed record QueryTranslationTests : IClassFixture<QueryingFixture>, IDisposable
{
    private const string Untranslatable =
        "Queries that cannot be translated to SQL throw instead of running in memory.";

    private readonly QueryingFixture _fixture;

    private readonly RecordingConnection _connection;

    public QueryTranslationTests(QueryingFixture fixture)
    {
        _fixture = fixture;
        _connection = new RecordingConnection(fixture.Connection);
    }

    private IQueryable<IRow> Customers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Customers];

    private IQueryable<IRow> Orders =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _connection)[_fixture.Orders];

    private IQueryable<IRow> Notes =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Crm, _connection)[_fixture.Notes];

    [Fact]
    public void FiltersRowsInOneStatement()
    {
        IRow row = Assert.Single(
            Customers.Where(x => x.Cells[_fixture.CustomerName].Value.TextValue == "Bob")
        );

        Assert.Equal("2", row.Cells[_fixture.CustomerId].Value.TextValue);
        Assert.Contains(" WHERE ", Assert.Single(_connection.Statements));
    }

    [Fact]
    public void ReadsRowsWithTheCellsOfTheTable()
    {
        IRow translated = Assert.Single(
            Customers.Where(x => x.Cells[_fixture.CustomerId].Value.TextValue == "1")
        );

        IRow read = Customers
            .AsEnumerable()
            .Single(x => x.Cells[_fixture.CustomerId].Value.TextValue == "1");

        Assert.Equal(
            _fixture.Customers.Columns.Select(x => read.Cells[x].Value.TextValue),
            _fixture.Customers.Columns.Select(x => translated.Cells[x].Value.TextValue)
        );
    }

    [Fact]
    public void ProjectsOrderedCellTextInOneStatement()
    {
        string[] names =
        [
            .. Customers
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .OrderByDescending(x => x),
        ];

        Assert.Equal(["Cid", "Bob", "Ann"], names);
        Assert.Contains(" ORDER BY ", Assert.Single(_connection.Statements));
    }

    [Fact]
    public void ReadsBooleanCellsAsTheirText()
    {
        string[] names =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .OrderBy(x => x),
        ];

        Assert.Equal(["Ann", "Cid"], names);
        Assert.Contains(" WHERE ", Assert.Single(_connection.Statements));
    }

    [Fact]
    public void ReadsNumberAndUuidCellsAsTheirText()
    {
        Assert.Equal(
            [1, 1, 1, 1, 1, 2],
            [
                Orders.Count(x => x.Cells[_fixture.OrderTotal].Value.TextValue == "250"),
                Orders.Count(x =>
                    x.Cells[_fixture.OrderCode].Value.TextValue
                    == "00000000-0000-0000-0000-00000000000b"
                ),
                Orders.Count(x => x.Cells[_fixture.OrderUnits].Value.TextValue == "7"),
                Orders.Count(x => x.Cells[_fixture.OrderPriority].Value.TextValue == "2"),
                Orders.Count(x => x.Cells[_fixture.OrderShelf].Value.TextValue == "20"),
                Orders.Count(x => x.Cells[_fixture.OrderCustomer].Value.TextValue == "1"),
            ]
        );
        Assert.Equal(6, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains("COUNT(", x));
    }

    [Fact]
    public void CountsRowsInSql()
    {
        Assert.Equal(3, Customers.Count());
        Assert.Equal(3L, Customers.LongCount());
        Assert.All(_connection.Statements, x => Assert.Contains("COUNT(", x));
    }

    [Fact]
    public void AnswersAnyAllAndContainsInSql()
    {
        Assert.True(
            Customers.Any(x => x.Cells[_fixture.CustomerName].Value.TextValue == "Bob")
        );
        Assert.False(
            Customers.All(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
        );
        Assert.True(
            Customers
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .Contains("Cid")
        );
        Assert.Equal(3, _connection.Statements.Count);
        Assert.DoesNotContain(
            _connection.Statements,
            x => !x.Contains("EXISTS") && !x.Contains(" IN ")
        );
    }

    [Fact]
    public void ReturnsElementsInSql()
    {
        IQueryable<IRow> ordered = Customers.OrderBy(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue
        );

        Assert.Equal(
            ["Ann", "Cid", "Bob", "Bob", "Bob"],
            [
                ordered.First().Cells[_fixture.CustomerName].Value.TextValue,
                ordered.Last().Cells[_fixture.CustomerName].Value.TextValue,
                ordered.ElementAt(1).Cells[_fixture.CustomerName].Value.TextValue,
                ordered
                    .ElementAtOrDefault(1)!
                    .Cells[_fixture.CustomerName]
                    .Value.TextValue,
                ordered
                    .Single(x => x.Cells[_fixture.CustomerId].Value.TextValue == "2")
                    .Cells[_fixture.CustomerName]
                    .Value.TextValue,
            ]
        );
        Assert.Equal(5, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains(" LIMIT ", x));
    }

    [Fact(Skip = Untranslatable)]
    public void ReturnsDefaultsWhenNoElementIsFound()
    {
        IQueryable<IRow> none = Customers.Where(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue == "Zed"
        );

        Assert.Null(none.FirstOrDefault());
        Assert.Null(none.LastOrDefault());
        Assert.Null(none.SingleOrDefault());
        Assert.Null(Customers.ElementAtOrDefault(5));
        Assert.Equal(4, _connection.Statements.Count);
    }

    [Fact]
    public void ThrowsWhenNoElementIsFound()
    {
        IQueryable<IRow> none = Customers.Where(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue == "Zed"
        );

        _ = Assert.Throws<InvalidOperationException>(() => none.First());
        _ = Assert.Throws<InvalidOperationException>(() => none.Single());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Customers.ElementAt(5));
    }

    [Fact]
    public void ThrowsWhenSingleFindsManyRows()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            Customers.Single(x =>
                x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
            )
        );
    }

    [Fact]
    public void AggregatesInSql()
    {
        IQueryable<int> lengths = Customers.Select(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue.Length
        );

        Assert.Equal(9, lengths.Sum());
        Assert.Equal(3, lengths.Max());
        Assert.Equal(
            "Ann",
            Customers.Min(x => x.Cells[_fixture.CustomerName].Value.TextValue)
        );
        Assert.Equal(3, _connection.Statements.Count);
    }

    [Fact]
    public void ThrowsWhenAggregatingNoValues()
    {
        IQueryable<int> none = Customers
            .Where(x => x.Cells[_fixture.CustomerName].Value.TextValue == "Zed")
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue.Length);

        Assert.Equal(
            "Sequence contains no elements",
            Assert.Throws<InvalidOperationException>(() => none.Max()).Message
        );
        Assert.Null(none.Select(x => (int?)x).Max());
    }

    [Fact]
    public void ReadsScalarResultsOfOtherTypes()
    {
        IQueryable<IRow> ann = Customers.Where(x =>
            x.Cells[_fixture.CustomerName].Value.TextValue == "Ann"
        );

        Assert.Equal(5L, ann.Select(x => 5L).Single());
        Assert.Null(ann.Select(x => (string?)null).Single());
        Assert.Equal(
            [3, 3, 3],
            Customers.Select(x =>
                (int?)x.Cells[_fixture.CustomerName].Value.TextValue.Length
            )
        );
        Assert.Equal(3, _connection.Statements.Count);
    }

    [Fact]
    public void ReadsTextOfNumberAndUuidCellsInSql()
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

        int[] translated =
        [
            .. columns.Select(column =>
                Orders.Sum(x => x.Cells[column].Value.TextValue.Length)
            ),
        ];

        Assert.Equal([3, 8, 108, 6, 3, 7], translated);
        Assert.Equal(6, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains("SUM(", x));
    }

    [Fact]
    public void ReadsTablesWithoutSchemaName()
    {
        IQueryable<IRow> notes = new PostgreSqlStoredTableDataSet(
            _fixture.Notes,
            new EmptyString(),
            _connection
        );

        Assert.Equal(
            "public",
            notes
                .Where(x => x.Cells[_fixture.NoteCustomer].Value.TextValue == "2")
                .Select(x => x.Cells[_fixture.NoteText].Value.TextValue)
                .Single()
        );
        Assert.Contains(" WHERE ", Assert.Single(_connection.Statements));
    }

    [Fact(Skip = Untranslatable)]
    public void ReadsQueriesHeldByRowsInMemory()
    {
        IQueryable<IRow> notes = Notes;

        string[] customers =
        [
            .. Customers
                .Select(customer => new { Customer = customer, Notes = notes })
                .SelectMany(
                    x =>
                        x.Notes.Where(note =>
                            note.Cells[_fixture.NoteCustomer].Value.TextValue
                            == x.Customer.Cells[_fixture.CustomerId].Value.TextValue
                        ),
                    (x, note) =>
                        x.Customer.Cells[_fixture.CustomerName].Value.TextValue
                        + ":"
                        + note.Cells[_fixture.NoteText].Value.TextValue
                )
                .OrderBy(x => x),
        ];

        Assert.Equal(["Ann:vip", "Cid:new"], customers);
    }

    [Fact]
    public void JoinsTablesOfOneSchemaInOneStatement()
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
                            Total = order.Cells[_fixture.OrderTotal].Value.TextValue,
                        }
                )
                .AsEnumerable()
                .Select(x => $"{x.Name}:{x.Total}")
                .Order(),
        ];

        Assert.Equal(["Ann:100", "Ann:250", "Cid:40"], totals);
        Assert.Contains(" JOIN ", Assert.Single(_connection.Statements));
    }

    [Fact]
    public void ReadsValueTuples()
    {
        (string, bool)[] customers =
        [
            .. Customers
                .Select(x => new ValueTuple<string, bool>(
                    x.Cells[_fixture.CustomerName].Value.TextValue,
                    x.Cells[_fixture.CustomerActive].Value.TextValue == "True"
                ))
                .OrderBy(x => x.Item1),
        ];

        Assert.Equal([("Ann", true), ("Bob", false), ("Cid", true)], customers);
        _ = Assert.Single(_connection.Statements);
    }

    [Fact(Skip = Untranslatable)]
    public void JoinsTablesOfDifferentSchemasInMemory()
    {
        string[] notes =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                .Join(
                    Notes.Where(x =>
                        x.Cells[_fixture.NoteText].Value.TextValue != "none"
                    ),
                    x => x.Cells[_fixture.CustomerId].Value.TextValue,
                    x => x.Cells[_fixture.NoteCustomer].Value.TextValue,
                    (customer, note) =>
                        customer.Cells[_fixture.CustomerName].Value.TextValue
                        + ":"
                        + note.Cells[_fixture.NoteText].Value.TextValue
                )
                .OrderBy(x => x),
        ];

        Assert.Equal(["Ann:vip", "Cid:new"], notes);
        Assert.Equal(2, _connection.Statements.Count);
        Assert.All(_connection.Statements, x => Assert.Contains(" WHERE ", x));
    }

    [Fact(Skip = Untranslatable)]
    public void JoinsTablesOfDifferentConnectionsInMemory()
    {
        RecordingConnection other = new(_fixture.Connection);

        int count = Customers
            .Join(
                new PostgreSqlStoredSchemaDataSet(_fixture.Shop, other)[_fixture.Orders],
                x => x.Cells[_fixture.CustomerId].Value.TextValue,
                x => x.Cells[_fixture.OrderCustomer].Value.TextValue,
                (customer, order) => order
            )
            .Count();

        Assert.Equal(3, count);
        _ = Assert.Single(_connection.Statements);
        _ = Assert.Single(other.Statements);
    }

    [Fact(Skip = Untranslatable)]
    public void FiltersByCorrelatedQueryOfAnotherSchemaInMemory()
    {
        IQueryable<IRow> notes = Notes;

        string[] names =
        [
            .. Customers
                .Where(customer =>
                    notes.Any(note =>
                        note.Cells[_fixture.NoteCustomer].Value.TextValue
                        == customer.Cells[_fixture.CustomerId].Value.TextValue
                    )
                )
                .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
                .OrderBy(x => x),
        ];

        Assert.Equal(["Ann", "Cid"], names);
        Assert.DoesNotContain(_connection.Statements, x => x.Contains(" WHERE "));
    }

    [Fact]
    public void TranslatesCapturedQueriesOfTheSameSchema()
    {
        IQueryable<IRow> large = Orders.Where(x =>
            x.Cells[_fixture.OrderTotal].Value.TextValue == "250"
        );

        string name = Customers
            .Where(customer =>
                large.Any(order =>
                    order.Cells[_fixture.OrderCustomer].Value.TextValue
                    == customer.Cells[_fixture.CustomerId].Value.TextValue
                )
            )
            .Select(x => x.Cells[_fixture.CustomerName].Value.TextValue)
            .Single();

        Assert.Equal("Ann", name);
        Assert.Contains("EXISTS", Assert.Single(_connection.Statements));
    }

    [Fact]
    public void ReadsUnionsOfOneTableInOneStatement()
    {
        IRow[] rows =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerName].Value.TextValue == "Ann")
                .Union(
                    Customers.Where(x =>
                        x.Cells[_fixture.CustomerName].Value.TextValue == "Bob"
                    )
                ),
        ];

        Assert.Equal(2, rows.Length);
        Assert.Contains(" UNION ", Assert.Single(_connection.Statements));
    }

    [Fact(Skip = Untranslatable)]
    public void ConcatenatesRowsOfDifferentTablesInMemory()
    {
        IRow[] rows = [.. Customers.Concat(Orders)];

        Assert.Equal(6, rows.Length);
        Assert.Equal(2, _connection.Statements.Count);
    }

    [Fact(Skip = Untranslatable)]
    public void ReadsCellsOfUnsupportedColumnTypesInMemory()
    {
        IRow row = Assert.Single(
            Customers.Where(x =>
                x.Cells[_fixture.CustomerBorn]
                    .Value.TextValue.StartsWith("01/01/2000", StringComparison.Ordinal)
            )
        );

        Assert.Equal("Ann", row.Cells[_fixture.CustomerName].Value.TextValue);
        Assert.DoesNotContain(" WHERE ", Assert.Single(_connection.Statements));
    }

    [Fact(Skip = Untranslatable)]
    public void RunsUntranslatableOperatorsInMemoryOverTranslatedSources()
    {
        string[] names =
        [
            .. Customers
                .Where(x => x.Cells[_fixture.CustomerActive].Value.TextValue == "True")
                .Select(x => Shout(x.Cells[_fixture.CustomerName].Value.TextValue))
                .OrderBy(x => x),
        ];

        Assert.Equal(["ANN!", "CID!"], names);
        Assert.Contains(" WHERE ", Assert.Single(_connection.Statements));
    }

    [Fact(Skip = Untranslatable)]
    public void ReadsOtherRowMembersInMemory()
    {
        Assert.Equal(3, Customers.Count(x => x.Cells.Count == 4));
        Assert.Equal(
            3,
            Customers.Count(x => x.Cells[x.Cells.Keys.First()].Value.TextValue != "")
        );
        Assert.DoesNotContain(_connection.Statements, x => x.Contains("COUNT("));
    }

    [Fact]
    public void ReadsColumnsOfOtherTablesInMemory()
    {
        IColumn text = _fixture.NoteText;

        _ = Assert.ThrowsAny<Exception>(() =>
            Customers.Count(x => x.Cells[text].Value.TextValue == "vip")
        );
        Assert.DoesNotContain(_connection.Statements, x => x.Contains("COUNT("));
    }

    [Fact(Skip = Untranslatable)]
    public void ExecutesThroughTheProvider()
    {
        IQueryable rows = Customers.Provider.CreateQuery(Customers.Expression);

        Assert.Equal(typeof(IRow), Customers.ElementType);
        Assert.Equal(typeof(IRow), rows.ElementType);
        Assert.Equal(3, rows.Cast<IRow>().AsEnumerable().Count());
        Assert.Equal(3, Count(rows));
        Assert.Equal(
            3,
            Customers.Provider.Execute(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Count),
                    [typeof(IRow)],
                    Customers.Expression
                )
            )
        );
        Assert.Equal(
            3,
            Customers.Provider.Execute<IEnumerable<IRow>>(Customers.Expression).Count()
        );
    }

    [Fact]
    public void ThrowsOnTextOfTablesInsteadOfRecursingThroughTheirTree()
    {
        _ = Assert.Throws<NotSupportedException>(Customers.ToString);
        _ = Assert.Throws<NotSupportedException>(Customers.Expression.ToString);
    }

    [Fact(Skip = Untranslatable)]
    public void JoinsInMemoryWhenResultsHoldRows()
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
                            Customer = customer,
                            Total = order.Cells[_fixture.OrderTotal].Value.TextValue,
                        }
                )
                .AsEnumerable()
                .Select(x =>
                    $"{x.Customer.Cells[_fixture.CustomerName].Value.TextValue}:{x.Total}"
                )
                .Order(),
        ];

        Assert.Equal(["Ann:100", "Ann:250", "Cid:40"], totals);
        Assert.Equal(2, _connection.Statements.Count);
        Assert.DoesNotContain(_connection.Statements, x => x.Contains(" JOIN "));
    }

    [Fact]
    public void TranslatesStructInitializersInSql()
    {
        string[] customers =
        [
            .. Customers
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[_fixture.CustomerName].Value.TextValue,
                    ""
                )
                {
                    Item2 = x.Cells[_fixture.CustomerActive].Value.TextValue,
                })
                .AsEnumerable()
                .Select(x => x.Item1 + ":" + x.Item2)
                .Order(),
        ];

        Assert.Equal(["Ann:True", "Bob:False", "Cid:True"], customers);
        _ = Assert.Single(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenAnOperatorCannotBeTranslated()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Customers.Select(x => Shout(x.Cells[_fixture.CustomerName].Value.TextValue))
                .ToArray()
        );

        Assert.Equal(
            "The query operator Select cannot be translated to SQL.",
            exception.Message
        );
        _ = Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Empty(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenAQueryReadsTablesOfDifferentSchemas()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Customers.Concat(Notes).Count()
        );

        Assert.Equal(
            "The query operator Count reads tables of more than one schema or "
                + "connection, so it cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenAQueryReadsTablesOfDifferentConnections()
    {
        using RecordingConnection other = new(_fixture.Connection);
        IQueryable<IRow> customers = new PostgreSqlStoredSchemaDataSet(
            _fixture.Shop,
            other
        )[_fixture.Customers];

        _ = Assert.Throws<NotSupportedException>(() =>
            Customers.Concat(customers).Count()
        );
        Assert.Empty(_connection.Statements);
        Assert.Empty(other.Statements);
    }

    [Fact]
    public void ThrowsWhenAQueryReadsSourcesOtherThanStoredTables()
    {
        IQueryable<IRow> rows = Array.Empty<IRow>().AsQueryable();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Customers.Concat(rows).Count()
        );

        Assert.Equal(
            "The query operator Count reads sources other than stored tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenAResultCannotBeRead()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            Customers.Select(x => new { Customer = x }).ToArray()
        );

        Assert.StartsWith(
            "The query operator Select returns ",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            ", which cannot be read from a SQL result.",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenRowsHoldQueriesOfAnotherSchema()
    {
        IQueryable<IRow> notes = Notes;

        _ = Assert.Throws<NotSupportedException>(() =>
            Customers
                .Select(customer => new { Customer = customer, Notes = notes })
                .SelectMany(
                    x =>
                        x.Notes.Where(note =>
                            note.Cells[_fixture.NoteCustomer].Value.TextValue
                            == x.Customer.Cells[_fixture.CustomerId].Value.TextValue
                        ),
                    (x, note) => note.Cells[_fixture.NoteText].Value.TextValue
                )
                .ToArray()
        );
        Assert.Empty(_connection.Statements);
    }

    [Fact]
    public void ThrowsWhenAnExpressionIsNotAQueryOperator()
    {
        Assert.Equal(
            "The query is not a query operator over stored tables, so it cannot be "
                + "translated to SQL.",
            Assert.Throws<NotSupportedException>(() =>
                Customers.Provider.Execute(Expression.Constant(3))
            ).Message
        );
    }

    [Fact]
    public void ReadsTablesThroughTheProvider()
    {
        Assert.Equal(
            3,
            Customers.Provider.Execute<IEnumerable<IRow>>(Customers.Expression).Count()
        );
        Assert.Equal(
            3,
            Customers.Provider.Execute(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Count),
                    [typeof(IRow)],
                    Customers.Expression
                )
            )
        );
        IQueryable rows = Customers.Provider.CreateQuery(Customers.Expression);

        Assert.Equal(typeof(IRow), rows.ElementType);
        Assert.Equal(3, Count(rows));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static int Count(IEnumerable rows)
    {
        int count = 0;
        IEnumerator enumerator = rows.GetEnumerator();
        while (enumerator.MoveNext())
        {
            count++;
        }
        return count;
    }

    private static string Shout(string text)
    {
        return text.ToUpperInvariant() + "!";
    }
}
