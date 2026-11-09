using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of parses that are not read as enumeration gives them yet are skipped
// until they are.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A cell parsed to a type must be the value the same parse gives over the
/// enumerated rows in memory, whatever the styles of the parse and the type parsed
/// to: the column is read in its place only where its value is that of the parse.
/// </summary>
public sealed record ParsedCellStyleTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public ParsedCellStyleTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Unsigned => _fixture.Unsigned;

    private IColumn Huge => _fixture.Huge;

    private IColumn Moment => _fixture.Moment;

    private IColumn ParentKey => _fixture.ParentKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ParsesCellsWithStylesThatKeepTheirValue()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => new ValueTuple<int, long, decimal>(
                    int.Parse(
                        x.Cells[Number].Value.TextValue,
                        NumberStyles.AllowLeadingSign
                    ),
                    long.Parse(
                        x.Cells[Small].Value.TextValue,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture
                    ),
                    decimal.Parse(
                        x.Cells[Huge].Value.TextValue,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture
                    )
                ))
        );
    }

    [Fact]
    public void ReadsThePartsOfParsedDateTimes()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => new ValueTuple<int, int, int, int, int>(
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .Year,
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .Month,
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .Day,
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .DayOfYear,
                    (int)
                        DateTime
                            .Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                            .DayOfWeek
                ))
        );
    }

    [Fact]
    public void ShiftsParsedDateTimes()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .Select(x => new ValueTuple<DateTime, DateTime, DateTime, TimeSpan>(
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .AddDays(30)
                        .AddMonths(1),
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .AddHours(25.5),
                    DateTime
                        .Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        )
                        .AddYears(-1),
                    DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ) - new DateTime(1999, 12, 31)
                ))
        );
    }

    [Fact(Skip = "#77: The text is cast whatever the number styles (translator #104).")]
    public void ParsesCellsAsHexadecimalNumbers()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => new ValueTuple<int, long>(
                    int.Parse(x.Cells[Small].Value.TextValue, NumberStyles.HexNumber),
                    long.Parse(
                        x.Cells[Small].Value.TextValue,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture
                    )
                ))
        );
    }

    [Fact(Skip = "#77: The text is cast whatever the number styles (translator #104).")]
    public void ParsesGuardedCellsAsHexadecimalNumbers()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x =>
                string.IsNullOrEmpty(x.Cells[Small].Value.TextValue)
                    ? null
                    : (int?)
                        int.Parse(x.Cells[Small].Value.TextValue, NumberStyles.HexNumber)
            )
        );
    }

    [Fact(Skip = "#77: The text is cast whatever the number styles (translator #104).")]
    public void FiltersRowsByCellsParsedAsHexadecimalNumbers()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Where(x =>
                    int.Parse(x.Cells[Small].Value.TextValue, NumberStyles.HexNumber)
                    == 0x65535
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact(Skip = "#77: The text is cast whatever the number styles (translator #104).")]
    public void ThrowsOnNegativeCellsParsedWithoutASign()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x =>
                    int.Parse(x.Cells[Number].Value.TextValue, NumberStyles.None)
                )
        );
    }

    [Fact(Skip = "#78: The timestamp read has no kind (translator #105).")]
    public void KeepsTheKindOfDateTimesParsedAsUniversal()
    {
        Enumeration.AssertSameValue(db =>
            string.Join(
                ", ",
                db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                    .Select(x =>
                        DateTime.Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal
                                | DateTimeStyles.AdjustToUniversal
                        )
                    )
                    .AsEnumerable()
                    .Select(x => x.Kind)
            )
        );
    }

    [Fact]
    public void ParsesCellsOfUShortColumnsAsUShorts()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => ushort.Parse(x.Cells[Small].Value.TextValue))
        );
    }

    [Fact]
    public void ParsesCellsOfUIntColumnsAsUInts()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => uint.Parse(x.Cells[Unsigned].Value.TextValue))
        );
    }

    [Fact]
    public void ParsesCellsOfULongColumnsAsULongs()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values).Select(x => ulong.Parse(x.Cells[Huge].Value.TextValue))
        );
    }

    [Fact]
    public void FiltersRowsByCellsParsedAsUnsignedNumbers()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Where(x =>
                    ushort.Parse(x.Cells[Small].Value.TextValue) > 5
                    && uint.Parse(x.Cells[Unsigned].Value.TextValue) > 5u
                    && ulong.Parse(x.Cells[Huge].Value.TextValue) > 5ul
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsUnsignedNumbersAndBytes()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new ValueTuple<uint, ulong, ushort, byte>(
                (uint)x.Cells[ParentKey].Value.TextValue.Length,
                (ulong)x.Cells[ParentKey].Value.TextValue.Length,
                (ushort)x.Cells[ParentKey].Value.TextValue.Length,
                (byte)x.Cells[ParentKey].Value.TextValue.Length
            ))
        );
    }

    private IQueryable<IRow> WithoutNull(IQueryable<IRow> rows)
    {
        return rows.Where(x => !x.Cells[ValueId].Value.TextValue.StartsWith('9'));
    }
}
