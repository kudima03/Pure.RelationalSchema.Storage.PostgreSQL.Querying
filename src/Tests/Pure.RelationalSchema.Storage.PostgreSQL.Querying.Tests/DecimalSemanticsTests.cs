using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Translated <see cref="decimal"/> arithmetic must give the values
/// <see cref="decimal"/> gives over the enumerated rows: a whole decimal
/// constant is a decimal, not an integer; a quotient, a product and a mean keep 28
/// significant digits, rounded as .NET rounds them; and a result past the range or
/// the precision of a decimal fails, or is rounded, as it is in memory. The values
/// are compared without their trailing zeros, which PostgreSQL keeps in the scale
/// of a <c>numeric</c> and .NET trims (<see cref="QuerySemanticsTests"/>).
/// </summary>
public sealed record DecimalSemanticsTests : IClassFixture<ExecutionFixture>
{
    private const string ExactNumeric =
        "PostgreSQL keeps a sum or a product of numerics exact where a decimal rounds "
        + "it to its 28 or 29 significant digits, and the exact value is read into no "
        + "decimal.";

    private readonly ExecutionFixture _fixture;

    public DecimalSemanticsTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Huge => _fixture.Huge;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void DividesWholeDecimalsOfAnInMemorySequenceAsDecimals()
    {
        decimal[] wholes = [250m, -250m, 350m];

        Enumeration.AssertSameValue(db =>
            Normalized(
                One(db.Values)
                    .SelectMany(
                        x => wholes,
                        (row, whole) =>
                            (
                                Math.Round(whole / 100m, MidpointRounding.AwayFromZero)
                                * 100m
                            ) + (whole / 100m)
                    )
            )
        );
    }

    [Fact]
    public void DividesDecimalsExactlyWhereTheQuotientEnds()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x => int.Parse(x.Cells[Number].Value.TextValue) / 100m)
            )
        );
    }

    [Fact]
    public void DividesAWholeDecimalConstantAsADecimal()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                Signed(db.Values).Select(x => x / 100m)
            )
        );
    }

    [Fact]
    public void RoundsAWholeDecimalConstantToHundredsAwayFromZero()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                Signed(db.Values)
                    .Select(x =>
                        Math.Round(x / 100m, MidpointRounding.AwayFromZero) * 100m
                    )
            )
        );
    }

    [Fact]
    public void RoundsAWholeDecimalConstantToHundredsToEven()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                Signed(db.Values)
                    .Select(x => Math.Round(x / 100m, MidpointRounding.ToEven) * 100m)
            )
        );
    }

    [Fact]
    public void FailsOnASumPastTheRangeOfADecimal()
    {
        decimal big = 9999999999999999999999999999m;

        Enumeration.AssertSameValue(db => WithoutNull(db.Values).Sum(x => big));
    }

    [Fact(Skip = ExactNumeric)]
    public void RoundsAnAdditionPastThePrecisionOfADecimal()
    {
        decimal tiny = 0.0000000000000000000000000001m;

        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x => int.Parse(x.Cells[Small].Value.TextValue) + tiny)
            )
        );
    }

    [Fact(Skip = ExactNumeric)]
    public void RoundsAnAggregatedSumPastThePrecisionOfADecimal()
    {
        decimal tiny = 0.0000000000000000000000000001m;

        Enumeration.AssertSameValue(db =>
            WithoutNull(db.Values)
                .Sum(x => x.Cells[ValueId].Value.TextValue == "1" ? 9m : tiny)
        );
    }

    [Fact]
    public void DividesDecimalsTo28Digits()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x =>
                        1m / (int.Parse(x.Cells[Small].Value.TextValue) + 3)
                    )
            )
        );
    }

    [Fact]
    public void FiltersByADecimalQuotientTo28Digits()
    {
        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Where(x =>
                    1m / (int.Parse(x.Cells[Small].Value.TextValue) + 3)
                    == 0.3333333333333333333333333333m
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void DividesAWideDecimalTo28Digits()
    {
        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x =>
                        decimal.Parse(
                            x.Cells[Huge].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ) / 100m
                    )
            )
        );
    }

    [Fact]
    public void RoundsAQuotientJustAboveAMidpointToEven()
    {
        decimal fine = 250.0000000000000000000000001m;

        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x =>
                        Math.Round(
                            int.Parse(x.Cells[Small].Value.TextValue) * fine / 100m,
                            MidpointRounding.ToEven
                        )
                    )
            )
        );
    }

    [Fact(Skip = ExactNumeric)]
    public void MultipliesDecimalsTo28DecimalPlaces()
    {
        decimal factor = 0.111111111111111m;

        Enumeration.AssertSameValue(db =>
            Normalized(
                WithoutNull(db.Values)
                    .Select(x =>
                        int.Parse(x.Cells[Small].Value.TextValue) * factor * factor
                    )
            )
        );
    }

    [Fact]
    public void AveragesDecimalsTo28Digits()
    {
        Enumeration.AssertSameValue(db =>
            WithoutNull(db.Values)
                .Average(x => (decimal)int.Parse(x.Cells[Small].Value.TextValue))
        );
        Enumeration.AssertSameValue(db =>
            WithoutNull(db.Values)
                .Average(x => (decimal)long.Parse(x.Cells[Big].Value.TextValue))
        );
    }

    /// <summary>
    /// The values without their trailing zeros, in order of their text.
    /// </summary>
    private static string Normalized(IQueryable<decimal> values)
    {
        return string.Join(
            ", ",
            values
                .AsEnumerable()
                .Select(x => x.ToString("G29", CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The whole decimal constant 250 or -250 of each row, as its flag is set.
    /// </summary>
    private IQueryable<decimal> Signed(IQueryable<IRow> rows)
    {
        return WithoutNull(rows)
            .Select(x => x.Cells[Flag].Value.TextValue == "True" ? 250m : -250m);
    }

    private IQueryable<IRow> One(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[ValueId].Value.TextValue == "1");
    }

    private IQueryable<IRow> WithoutNull(IQueryable<IRow> rows)
    {
        return rows.Where(x => !x.Cells[ValueId].Value.TextValue.StartsWith('9'));
    }
}
