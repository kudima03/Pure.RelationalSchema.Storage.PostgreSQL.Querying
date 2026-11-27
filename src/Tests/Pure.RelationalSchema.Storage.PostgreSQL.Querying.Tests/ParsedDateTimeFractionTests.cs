using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A stored <c>datetime</c> parsed from its cell text assuming UTC,
/// <c>DateTime.Parse(text, invariant, AssumeUniversal | AdjustToUniversal)</c>, must
/// keep the fraction of a second the text writes
/// (<c>MM/dd/yyyy HH:mm:ss.FFFFFFF</c>), as the same parse over the enumerated rows
/// does, wherever the value is read, compared, shifted, subtracted, aggregated or
/// ordered; and a whole second, written without a point, must still parse.
/// </summary>
public sealed record ParsedDateTimeFractionTests : IClassFixture<ExecutionFixture>
{
    private const string DropsTheFraction =
        "#110 (translator #146): DateTime.Parse(text, invariant, AssumeUniversal | "
        + "AdjustToUniversal) of a stored datetime is TO_TIMESTAMP('+00 ' || text, "
        + "'TZH MM/DD/YYYY HH24:MI:SS'), whose pattern drops the fraction of a second: ";

    private static readonly DateTimeStyles Universal =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly ExecutionFixture _fixture;

    public ParsedDateTimeFractionTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Moment => _fixture.Moment;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ParsesStoredDateTimesOfWholeSecondsAsUniversal()
    {
        Enumeration.AssertSameElements(db =>
            Universally(
                db.Values.Where(x => !x.Cells[Moment].Value.TextValue.Contains('.'))
            )
        );
    }

    [Fact(
        Skip = DropsTheFraction
            + "9999-12-31T23:59:59.999999Z is read as ...23:59:59Z."
    )]
    public void ReadsStoredDateTimesParsedAsUniversalWithTheirFraction()
    {
        Enumeration.AssertSameElements(db => Universally(db.Values));
    }

    [Fact(
        Skip = DropsTheFraction
            + "the milliseconds of ...12:34:56.12Z are 0, not 120."
    )]
    public void ReadsTheMillisecondsOfStoredDateTimesParsedAsUniversal()
    {
        Enumeration.AssertSameElements(db =>
            Universally(db.Values)
                .Select(x => new ValueTuple<string, int>(x.Item1, x.Item2.Millisecond))
        );
    }

    [Fact(
        Skip = DropsTheFraction
            + "equality with 2024-12-31T12:34:56.12Z keeps no row."
    )]
    public void FindsAStoredDateTimeParsedAsUniversalByItsFraction()
    {
        DateTime moment = new DateTime(2024, 12, 31, 12, 34, 56, 120, DateTimeKind.Utc);

        Enumeration.AssertSameElements(db =>
            Universally(db.Values).Where(x => x.Item2 == moment).Select(x => x.Item1)
        );
    }

    [Fact(Skip = DropsTheFraction + "> 1999-12-31T23:59:59.5Z drops ...23:59:59.9Z.")]
    public void ComparesStoredDateTimesParsedAsUniversalByTheirFraction()
    {
        DateTime since = new DateTime(1999, 12, 31, 23, 59, 59, 500, DateTimeKind.Utc);
        DateTime last = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        Enumeration.AssertSameElements(db =>
            Universally(db.Values)
                .Where(x => (x.Item2 > since && x.Item2.Year < 2000) || x.Item2 > last)
                .Select(x => x.Item1)
        );
    }

    [Fact(Skip = DropsTheFraction + "differences and shifts lose it.")]
    public void SubtractsAndShiftsStoredDateTimesParsedAsUniversal()
    {
        DateTime since = new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        Enumeration.AssertSameElements(db =>
            Universally(db.Values)
                .Where(x => x.Item2.Year > 1 && x.Item2.Year < 9999)
                .Select(x => new ValueTuple<string, TimeSpan, DateTime, DateTime>(
                    x.Item1,
                    x.Item2 - since,
                    x.Item2.AddSeconds(1.5),
                    x.Item2.AddMonths(1)
                ))
        );
    }

    [Fact(
        Skip = DropsTheFraction
            + "the latest is ...23:59:59Z, not ...23:59:59.999999Z."
    )]
    public void TakesTheLatestStoredDateTimeParsedAsUniversal()
    {
        Enumeration.AssertSameValue(db => Universally(db.Values).Max(x => x.Item2));
    }

    [Fact(
        Skip = DropsTheFraction
            + "moments a fraction apart tie, so the ids break the tie."
    )]
    public void OrdersStoredDateTimesParsedAsUniversalByTheirFraction()
    {
        Enumeration.AssertSameOrder(db =>
            Universally(db.Values)
                .OrderBy(x => x.Item2)
                .ThenBy(x => x.Item1)
                .Select(x => x.Item1)
        );
    }

    /// <summary>
    /// The id of each row with a moment, and the moment parsed assuming UTC.
    /// </summary>
    private IQueryable<ValueTuple<string, DateTime>> Universally(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[Moment].Value.TextValue != "")
            .Select(x => new ValueTuple<string, DateTime>(
                x.Cells[ValueId].Value.TextValue,
                DateTime.Parse(
                    x.Cells[Moment].Value.TextValue,
                    CultureInfo.InvariantCulture,
                    Universal
                )
            ));
    }
}
