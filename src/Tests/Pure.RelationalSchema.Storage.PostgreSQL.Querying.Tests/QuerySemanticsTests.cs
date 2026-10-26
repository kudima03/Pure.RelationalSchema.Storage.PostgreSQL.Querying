using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of semantics translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Translated queries must return what the same query returns over the enumerated
/// rows in memory, or throw the same exception.
/// </summary>
public sealed record QuerySemanticsTests : IClassFixture<QueryingFixture>
{
    private readonly QueryingFixture _fixture;

    public QuerySemanticsTests(QueryingFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn WordText => _fixture.WordText;

    private IColumn CustomerName => _fixture.CustomerName;

    private IColumn EventId => _fixture.EventId;

    private IQueryable<IRow> Words =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
            _fixture.Words
        ];

    private IQueryable<IRow> Customers =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
            _fixture.Customers
        ];

    private IQueryable<IRow> Events =>
        new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
            _fixture.Events
        ];

    [Fact(Skip = "LENGTH counts code points, string.Length counts UTF-16 code units.")]
    public void CountsTextLengthInUtf16CodeUnits()
    {
        AssertLikeInMemory(
            Words,
            rows => Texts(rows, WordText, x => x.Length.ToString())
        );
    }

    [Fact(Skip = "STRPOS counts code points, IndexOf counts UTF-16 code units.")]
    public void IndexesTextInUtf16CodeUnits()
    {
        AssertLikeInMemory(
            Words,
            rows => Texts(rows, WordText, x => x.IndexOf('x').ToString())
        );
    }

    [Fact(Skip = "SUBSTR counts code points, Substring counts UTF-16 code units.")]
    public void TakesSubstringsInUtf16CodeUnits()
    {
        AssertLikeInMemory(
            Words,
            rows =>
                Texts(
                    rows.Where(x => x.Cells[WordText].Value.TextValue == "\U0001F600x"),
                    WordText,
                    x => x.Substring(2)
                )
        );
    }

    [Fact]
    public void MatchesUnderscoresLiterally()
    {
        AssertLikeInMemory(
            Words,
            rows => Ids(rows.Where(x => x.Cells[WordText].Value.TextValue.Contains('_')))
        );
    }

    [Fact]
    public void MatchesPercentSignsLiterally()
    {
        AssertLikeInMemory(
            Words,
            rows => Ids(rows.Where(x => x.Cells[WordText].Value.TextValue.EndsWith('%')))
        );
    }

    [Fact]
    public void MatchesBackslashesLiterally()
    {
        AssertLikeInMemory(
            Words,
            rows => Ids(rows.Where(x => x.Cells[WordText].Value.TextValue.Contains('\\')))
        );
    }

    [Fact]
    public void MatchesPrefixesWithBackslashesLiterally()
    {
        AssertLikeInMemory(
            Words,
            rows =>
                Ids(rows.Where(x => x.Cells[WordText].Value.TextValue.StartsWith("a\\b")))
        );
    }

    [Fact]
    public void ComparesTextCaseSensitively()
    {
        AssertLikeInMemory(
            Words,
            rows => Ids(rows.Where(x => x.Cells[WordText].Value.TextValue == "mixed"))
        );
    }

    [Fact]
    public void FindsEmptyText()
    {
        AssertLikeInMemory(
            Words,
            rows =>
                Ids(
                    rows.Where(x =>
                        string.IsNullOrEmpty(x.Cells[WordText].Value.TextValue)
                    )
                )
        );
    }

    [Fact]
    public void TrimsEveryWhiteSpaceCharacter()
    {
        AssertLikeInMemory(
            Words,
            rows => Texts(rows, WordText, x => "[" + x.Trim() + "]")
        );
    }

    [Fact]
    public void ConvertsCase()
    {
        AssertLikeInMemory(
            Words,
            rows =>
                Texts(rows, WordText, x => x.ToUpperInvariant() + x.ToLowerInvariant())
        );
    }

    [Fact]
    public void FiltersByMembershipInAnEmptyCollection()
    {
        string[] none = [];

        AssertLikeInMemory(
            Words,
            rows => Ids(rows.Where(x => none.Contains(x.Cells[WordText].Value.TextValue)))
        );
    }

    [Fact]
    public void FiltersByNonMembershipInAnEmptyCollection()
    {
        string[] none = [];

        AssertLikeInMemory(
            Words,
            rows =>
                Ids(rows.Where(x => !none.Contains(x.Cells[WordText].Value.TextValue)))
        );
    }

    [Fact]
    public void FiltersByNonMembershipInACollectionHoldingNull()
    {
        string?[] values = [null, "a_b"];

        AssertLikeInMemory(
            Words,
            rows =>
                Ids(rows.Where(x => !values.Contains(x.Cells[WordText].Value.TextValue)))
        );
    }

    [Fact]
    public void ComparesUuidTextCaseSensitively()
    {
        AssertLikeInMemory(
            OrdersTable(),
            rows =>
                rows.Count(x =>
                    x.Cells[_fixture.OrderCode].Value.TextValue
                    == "00000000-0000-0000-0000-00000000000A"
                )
        );
    }

    [Fact]
    public void ReadsDateTimeCellsAsTheirText()
    {
        AssertLikeInMemory(
            Events,
            rows => Times(rows.Where(x => x.Cells[EventId].Value.TextValue != "4"))
        );
    }

    [Fact]
    public void TruncatesFractionalSecondsOfDateTimeText()
    {
        AssertLikeInMemory(
            Events,
            rows => Times(rows.Where(x => x.Cells[EventId].Value.TextValue == "4"))
        );
    }

    [Fact(Skip = "Math.Round is not translated.")]
    public void RoundsDecimalsHalfToEven()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        Math.Round(
                            ((decimal)x.Cells[CustomerName].Value.TextValue.Length + 2)
                                / 2m
                        )
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "Math.Round is not translated.")]
    public void RoundsDoublesHalfToEven()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        Math.Round(
                            ((double)x.Cells[CustomerName].Value.TextValue.Length + 2) / 2
                        )
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact]
    public void TruncatesDoublesConvertedToIntegers()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        (int)(x.Cells[CustomerName].Value.TextValue.Length / 2.0)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact]
    public void DividesIntegersTowardZero()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        (-x.Cells[CustomerName].Value.TextValue.Length / 2)
                        + ":"
                        + (-x.Cells[CustomerName].Value.TextValue.Length % 2)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL raises integer out of range where C# wraps.")]
    public void WrapsOverflowingIntegerArithmetic()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        x.Cells[CustomerName].Value.TextValue.Length * int.MaxValue
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL raises its own error, not DivideByZeroException.")]
    public void ThrowsOnIntegerDivisionByZero()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        x.Cells[CustomerName].Value.TextValue.Length
                        / (x.Cells[CustomerName].Value.TextValue.Length - 3)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL raises division by zero where C# returns infinity.")]
    public void DividesDoublesByZeroToInfinity()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        (double)x.Cells[CustomerName].Value.TextValue.Length
                        / (x.Cells[CustomerName].Value.TextValue.Length - 3)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL raises an error where Math.Sqrt returns NaN.")]
    public void TakesSquareRootsOfNegativeNumbersAsNaN()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        Math.Sqrt(-(double)x.Cells[CustomerName].Value.TextValue.Length)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL raises an error where Math.Log returns -infinity.")]
    public void TakesLogarithmsOfZeroAsNegativeInfinity()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        Math.Log(x.Cells[CustomerName].Value.TextValue.Length - 3.0)
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL formats exponents as e+20, .NET as E+20.")]
    public void FormatsDoublesLikeDotNet()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        (x.Cells[CustomerName].Value.TextValue.Length * 1e20).ToString()
                        + "|"
                        + (x.Cells[CustomerName].Value.TextValue.Length / 10.0).ToString()
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "PostgreSQL keeps the numeric scale of a division, .NET trims it.")]
    public void FormatsDecimalsLikeDotNet()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Select(x =>
                        (x.Cells[CustomerName].Value.TextValue.Length / 3m).ToString()
                    )
                    .Distinct()
                    .ToArray()
        );
    }

    [Fact(Skip = "SUBSTR returns empty text where Substring throws.")]
    public void ThrowsOnSubstringsOutOfRange()
    {
        AssertLikeInMemory(
            Customers,
            rows => Texts(rows, CustomerName, x => x.Substring(5))
        );
    }

    [Fact(Skip = "REPLACE ignores empty text where Replace throws.")]
    public void ThrowsOnReplacingEmptyText()
    {
        AssertLikeInMemory(
            Customers,
            rows => Texts(rows, CustomerName, x => x.Replace("", "-"))
        );
    }

    [Fact]
    public void SumsNoRowsToZero()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Where(x => x.Cells[CustomerName].Value.TextValue == "Zed")
                    .Sum(x => x.Cells[CustomerName].Value.TextValue.Length)
        );
    }

    [Fact]
    public void ReturnsNullForTheMinimumOfNoText()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Where(x => x.Cells[CustomerName].Value.TextValue == "Zed")
                    .Min(x => x.Cells[CustomerName].Value.TextValue)
        );
    }

    [Fact]
    public void ThrowsOnTheAverageOfNoRows()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.Where(x => x.Cells[CustomerName].Value.TextValue == "Zed")
                    .Average(x => x.Cells[CustomerName].Value.TextValue.Length)
        );
    }

    [Fact]
    public void SkipsNoRowsForNegativeCounts()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                Names(rows.OrderBy(x => x.Cells[CustomerName].Value.TextValue).Skip(-1))
        );
    }

    [Fact]
    public void TakesNoRowsForNegativeCounts()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                Names(rows.OrderBy(x => x.Cells[CustomerName].Value.TextValue).Take(-1))
        );
    }

    [Fact]
    public void ThrowsOnNegativeElementIndexes()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                Name(
                    rows.OrderBy(x => x.Cells[CustomerName].Value.TextValue).ElementAt(-1)
                )
        );
    }

    [Fact]
    public void ReturnsDefaultsForNegativeElementIndexes()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                rows.OrderBy(x => x.Cells[CustomerName].Value.TextValue)
                    .ElementAtOrDefault(-1)
        );
    }

    [Fact]
    public void ThrowsOnElementIndexesPastTheEnd()
    {
        AssertLikeInMemory(
            Customers,
            rows =>
                Name(
                    rows.OrderBy(x => x.Cells[CustomerName].Value.TextValue).ElementAt(3)
                )
        );
    }

    [Fact(Skip = "The translator rejects Last without an ordering.")]
    public void ReturnsTheLastRowOfAnUnorderedQuery()
    {
        AssertLikeInMemory(Customers, rows => Name(rows.Last()));
    }

    private IQueryable<IRow> OrdersTable()
    {
        return new PostgreSqlStoredSchemaDataSet(_fixture.Shop, _fixture.Connection)[
            _fixture.Orders
        ];
    }

    private string Name(IRow row)
    {
        return row.Cells[CustomerName].Value.TextValue;
    }

    private string[] Ids(IQueryable<IRow> rows)
    {
        return
        [
            .. rows.Select(x => x.Cells[_fixture.WordId].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal),
        ];
    }

    private string[] Times(IQueryable<IRow> rows)
    {
        return
        [
            .. rows.Select(x => x.Cells[_fixture.EventAt].Value.TextValue)
                .AsEnumerable()
                .Order(StringComparer.Ordinal),
        ];
    }

    private string[] Names(IQueryable<IRow> rows)
    {
        return [.. rows.Select(x => x.Cells[CustomerName].Value.TextValue)];
    }

    private static string[] Texts(
        IQueryable<IRow> rows,
        IColumn column,
        Expression<Func<string, string>> selector
    )
    {
        return
        [
            .. rows.Select(x => x.Cells[column].Value.TextValue)
                .Select(selector)
                .AsEnumerable()
                .Order(StringComparer.Ordinal),
        ];
    }

    private static void AssertLikeInMemory<T>(
        IQueryable<IRow> table,
        Func<IQueryable<IRow>, T> query
    )
    {
        IQueryable<IRow> rows = new EnumerableQuery<IRow>([.. table.AsEnumerable()]);

        Assert.Equal(Outcome(() => query(rows)), Outcome(() => query(table)));
    }

    private static object? Outcome<T>(Func<T> query)
    {
        T? result = default;
        Exception? exception = Record.Exception(() => result = query());
        return exception?.GetType() ?? (object?)result;
    }
}
