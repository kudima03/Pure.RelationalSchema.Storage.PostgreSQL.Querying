using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A cell parsed to its type, <c>Guid.Parse(row.Cells[column].Value.TextValue)</c>
/// and the like, bare or guarded by <c>string.IsNullOrEmpty</c>, is read as the
/// column itself: the statement compares, joins and projects the column, with no
/// text in between, and returns what the same query returns over the enumerated
/// rows. The datetime and time cells compared with enumeration hold whole seconds
/// and minutes, as the cell text keeps no more while the column is read whole.
/// </summary>
public sealed record TypedCellTests : IClassFixture<ExecutionFixture>
{
    private static readonly DateTimeStyles Universal =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly ExecutionFixture _fixture;

    public TypedCellTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Huge => _fixture.Huge;

    private IColumn Code => _fixture.Code;

    private IColumn Moment => _fixture.Moment;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildNumber => _fixture.ChildNumber;

    [Fact]
    public void ReadsParsedCellsAsTheirValues()
    {
        AssertLikeEnumeration(db =>
            WithoutNull(db.Values, ValueId)
                .Select(x => new ValueTuple<string, bool, long, long, decimal, Guid>(
                    x.Cells[ValueId].Value.TextValue,
                    bool.Parse(x.Cells[Flag].Value.TextValue),
                    long.Parse(
                        x.Cells[Small].Value.TextValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture
                    ),
                    long.Parse(
                        x.Cells[Big].Value.TextValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture
                    ),
                    decimal.Parse(
                        x.Cells[Huge].Value.TextValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture
                    ),
                    Guid.Parse(x.Cells[Code].Value.TextValue)
                ))
        );
    }

    [Fact]
    public void ReadsParsedDateAndTimeCellsAsTheirValues()
    {
        string[] whole = ["1", "2", "6"];

        AssertLikeEnumeration(db =>
            db
                .Values.Where(x => whole.Contains(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<string, DateTime>(
                    x.Cells[ValueId].Value.TextValue,
                    DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture,
                        Universal
                    )
                ))
        );
        AssertLikeEnumeration(db =>
            WithoutNull(db.Extras, ExtraId)
                .Select(x => new ValueTuple<string, DateOnly, bool>(
                    x.Cells[ExtraId].Value.TextValue,
                    DateOnly.Parse(
                        x.Cells[Day].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ),
                    TimeOnly.Parse(
                        x.Cells[Clock].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ) >= new TimeOnly(3, 4)
                ))
        );
    }

    [Fact]
    public void ReadsGuardedParsedCellsAsNullableValues()
    {
        AssertLikeEnumeration(db =>
            db.Values.Select(x => new ValueTuple<string, Guid?, long?, int?>(
                x.Cells[ValueId].Value.TextValue,
                string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                    ? null
                    : Guid.Parse(x.Cells[Code].Value.TextValue),
                string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                    ? null
                    : long.Parse(
                        x.Cells[Number].Value.TextValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture
                    ),
                string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                    ? null
                    : int.Parse(
                        x.Cells[Number].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
            ))
        );
    }

    [Fact]
    public void TestsEmptyCellsOfTypedColumnsAsNull()
    {
        AssertLikeEnumeration(db =>
            db
                .Values.Where(x => string.IsNullOrEmpty(x.Cells[Code].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
        AssertLikeEnumeration(db =>
            db
                .Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersAndJoinsOnParsedCells()
    {
        DateOnly since = new(2000, 1, 1);
        Guid code = new("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");

        AssertLikeEnumeration(db =>
            WithoutNull(db.Extras, ExtraId)
                .Where(x =>
                    DateOnly.Parse(x.Cells[Day].Value.TextValue, CultureInfo.InvariantCulture)
                    >= since
                )
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        AssertLikeEnumeration(db =>
            WithoutNull(db.Values, ValueId)
                .Where(x => Guid.Parse(x.Cells[Code].Value.TextValue) == code)
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
        AssertLikeEnumeration(db =>
            WithoutNull(db.Parents, ParentId)
                .Join(
                    WithoutNull(db.Children, ChildId),
                    p => new ValueTuple<long>(
                        long.Parse(
                            p.Cells[ParentNumber].Value.TextValue,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture
                        )
                    ),
                    c => new ValueTuple<long>(
                        long.Parse(
                            c.Cells[ChildNumber].Value.TextValue,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture
                        )
                    ),
                    (p, c) => new ValueTuple<string, string>(
                        p.Cells[ParentId].Value.TextValue,
                        c.Cells[ChildId].Value.TextValue
                    )
                )
        );
    }

    [Fact]
    public void ComparesAndJoinsTheColumnsThemselves()
    {
        Guid code = new("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");

        string filtered = Statement(db =>
            db
                .Values.Where(x => Guid.Parse(x.Cells[Code].Value.TextValue) == code)
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
        string joined = Statement(db =>
            db.Parents.Join(
                db.Children,
                p => new ValueTuple<long>(
                    long.Parse(
                        p.Cells[ParentNumber].Value.TextValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture
                    )
                ),
                c => new ValueTuple<long>(
                    long.Parse(
                        c.Cells[ChildNumber].Value.TextValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture
                    )
                ),
                (p, c) => new ValueTuple<string, string>(
                    p.Cells[ParentId].Value.TextValue,
                    c.Cells[ChildId].Value.TextValue
                )
            )
        );
        string guarded = Statement(db =>
            db.Values.Select(x =>
                string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                    ? null
                    : (Guid?)Guid.Parse(x.Cells[Code].Value.TextValue)
            )
        );
        string tested = Statement(db =>
            db
                .Values.Where(x => string.IsNullOrEmpty(x.Cells[Code].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );

        Assert.DoesNotContain("CAST(", filtered.Split(" WHERE ")[1], StringComparison.Ordinal);
        Assert.DoesNotContain(
            "COALESCE",
            filtered.Split(" WHERE ")[1],
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("CAST(", joined.Split(" ON ")[1], StringComparison.Ordinal);
        Assert.DoesNotContain("IS NOT DISTINCT FROM", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("CAST(", guarded, StringComparison.Ordinal);
        Assert.DoesNotContain("CASE", guarded, StringComparison.Ordinal);
        Assert.EndsWith("\" IS NULL", tested, StringComparison.Ordinal);
    }

    private static IQueryable<IRow> WithoutNull(IQueryable<IRow> rows, IColumn id)
    {
        return rows.Where(x => !x.Cells[id].Value.TextValue.StartsWith('9'));
    }

    /// <summary>
    /// The one statement the query runs over the stored tables.
    /// </summary>
    private string Statement<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        _ = query(ExecutionTables.Stored(_fixture, connection)).ToList();

        return Assert.Single(connection.Statements);
    }

    /// <summary>
    /// The query returns the same elements, in any order, over the stored tables in
    /// one statement as over their enumerated rows.
    /// </summary>
    private void AssertLikeEnumeration<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        string[] expected = Outcome(ExecutionTables.Enumerated(_fixture));

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(ExecutionTables.Stored(_fixture, connection));

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
        _ = Assert.Single(connection.Statements);

        string[] Outcome(ExecutionTables tables)
        {
            return
            [
                .. query(tables)
                    .AsEnumerable()
                    .Select(x => string.Join("|", Values(x)))
                    .Order(StringComparer.Ordinal),
            ];
        }
    }

    private static IEnumerable<string> Values(object? element)
    {
        return element switch
        {
            null => [""],
            System.Runtime.CompilerServices.ITuple tuple => Enumerable
                .Range(0, tuple.Length)
                .SelectMany(index => Values(tuple[index])),
            IFormattable value => [value.ToString(null, CultureInfo.InvariantCulture)],
            _ => [element.ToString() ?? ""],
        };
    }
}
