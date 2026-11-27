using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A captured value, which the statement holds as a constant, must be what it is
/// in memory: a null of a nullable number is a null of that number, not an untyped
/// NULL PostgreSQL takes for text, and a <see cref="TimeOnly"/> keeps its ticks below
/// a microsecond, or is refused, when a value is computed from it.
/// </summary>
public sealed record CapturedValueEdgeTests : IClassFixture<ExecutionFixture>
{
    private const string UntypedNull =
        "#111 (translator #144): a null captured value is written as an untyped NULL, ";

    private readonly ExecutionFixture _fixture;

    public CapturedValueEdgeTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Huge => _fixture.Huge;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsANullCapturedValueInArithmeticAndComparisons()
    {
        long? none = null;

        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => new ValueTuple<string, long?, bool, long?>(
                    x.Cells[ValueId].Value.TextValue,
                    long.Parse(x.Cells[Big].Value.TextValue) + none,
                    long.Parse(x.Cells[Big].Value.TextValue) >= none,
                    x.Cells[Flag].Value.TextValue == "True" ? none : null
                ))
        );
        Enumeration.AssertSameValue(db => WithoutNull(db.Values).Max(x => none));
    }

    [Fact(
        Skip = UntypedNull
            + "so a conditional of two nulls compared with a number fails: operator "
            + "does not exist: text >= integer."
    )]
    public void ComparesAConditionalOfANullCapturedValue()
    {
        long? none = null;

        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x => new ValueTuple<string, bool>(
                    x.Cells[ValueId].Value.TextValue,
                    (x.Cells[Flag].Value.TextValue == "True" ? none : null) >= 0
                ))
        );
    }

    [Fact(
        Skip = UntypedNull
            + "so its sum fails: function sum(unknown) is not unique, where .NET "
            + "gives 0."
    )]
    public void SumsANullCapturedValue()
    {
        long? none = null;

        Enumeration.AssertSameValue(db => WithoutNull(db.Values).Sum(x => none));
    }

    [Fact(
        Skip = "#111 (translator #148): the translator refuses the Value of a null "
            + "captured int? in "
            + "the branch of a conditional its constant test never takes "
            + "(DeferredConstant.Value is not supported), where .NET gives null."
    )]
    public void RoundsToTheDigitsOfANullCapturedValue()
    {
        int? digits = null;

        Enumeration.AssertSameElements(db =>
            WithoutNull(db.Values)
                .Select(x =>
                    digits == null
                        ? (decimal?)null
                        : Math.Round(
                            decimal.Parse(
                                x.Cells[Huge].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                            digits.Value
                        )
                )
        );
    }

    [Fact]
    public void ShiftsACapturedTimeOfWholeMicroseconds()
    {
        TimeOnly time = new TimeOnly(23, 59, 59).Add(TimeSpan.FromTicks(9_999_990));

        Enumeration.AssertSameValue(db => Ticks(Shifted(db.Values, time)));
    }

    [Fact(
        Skip = "#111 (translator #144): a captured TimeOnly of 23:59:59.9999995 is "
            + "written as TIME '23:59:59.999999', so a value computed from it loses "
            + "the ticks below a microsecond."
    )]
    public void ShiftsACapturedTimeFinerThanAMicrosecond()
    {
        TimeOnly time = new TimeOnly(23, 59, 59).Add(TimeSpan.FromTicks(9_999_995));

        Enumeration.AssertSameValue(db => Ticks(Shifted(db.Values, time)));
    }

    /// <summary>
    /// The time shifted by as many hours as the number of the first row holds, none.
    /// </summary>
    private IQueryable<TimeOnly> Shifted(IQueryable<IRow> rows, TimeOnly time)
    {
        return rows.Where(x => x.Cells[ValueId].Value.TextValue == "1")
            .Select(x => time.AddHours(int.Parse(x.Cells[Number].Value.TextValue)));
    }

    /// <summary>
    /// The ticks of the times, which the text of a <see cref="TimeOnly"/> rounds.
    /// </summary>
    private static string Ticks(IQueryable<TimeOnly> times)
    {
        return string.Join(", ", times.AsEnumerable().Select(x => x.Ticks));
    }

    private IQueryable<IRow> WithoutNull(IQueryable<IRow> rows)
    {
        return rows.Where(x => !x.Cells[ValueId].Value.TextValue.StartsWith('9'));
    }
}
