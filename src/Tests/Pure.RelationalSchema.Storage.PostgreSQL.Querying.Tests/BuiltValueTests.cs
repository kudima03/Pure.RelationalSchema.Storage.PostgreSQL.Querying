using System.Globalization;
using System.Text;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Values built from the cells of stored rows with the .NET members the translator
/// writes as SQL — dates and times from their parts, <c>string.Join</c> of values,
/// groups and subqueries, the first and last elements of an ordered group, the
/// greatest and least of several values and <c>string.Normalize</c> — return in one
/// statement what they return over the enumerated rows.
/// </summary>
public sealed record BuiltValueTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public BuiltValueTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Moment => _fixture.Moment;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void BuildsDatesAndTimesFromThePartsOfCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => !string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue))
                .Select(x =>
                    DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
                )
                .Select(x => new ValueTuple<DateOnly, TimeOnly, DateTime, DateTime>(
                    new DateOnly(x.Year, x.Month, 1),
                    new TimeOnly(x.Hour, x.Minute, x.Second),
                    new DateTime(new DateOnly(x.Year, 1, x.Day), new TimeOnly(x.Hour, 0)),
                    new DateOnly(x.Year, x.Month, x.Day).ToDateTime(new TimeOnly(0, x.Minute))
                ))
        );
    }

    [Fact]
    public void BuildsDatesFromDayNumbersOfCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateOnly>(
                x.Cells[ValueId].Value.TextValue,
                DateOnly.FromDayNumber(
                    (int)long.Parse(x.Cells[ValueId].Value.TextValue) * 30000
                )
            ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfAnArray()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x =>
                string.Join(
                    " | ",
                    new[]
                    {
                        x.Cells[Text].Value.TextValue,
                        x.Cells[ValueId].Value.TextValue,
                        string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            ? null
                            : x.Cells[Flag].Value.TextValue,
                    }
                )
            )
        );
    }

    [Fact]
    public void JoinsTheOrderedTextsOfAGroup()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    ),
                    string.Join(
                        "",
                        x.OrderByDescending(row =>
                                long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                            .Select(row => row.Cells[Text].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheOrderedTextsOfASubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    "+",
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderByDescending(child =>
                            long.Parse(child.Cells[ChildId].Value.TextValue)
                        )
                        .Select(child => child.Cells[ChildLabel].Value.TextValue)
                )
            ))
        );
    }

    [Fact]
    public void ReadsTheFirstAndLastValuesOfOrderedGroups()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Text].Value.TextValue)
                        .First(),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Moment].Value.TextValue)
                        .First(),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last()
                ))
        );
    }

    // The translator takes a single value of an ordered group ('First' of a group is
    // translated for a single value only), not a row.
    [Fact]
    public void RefusesTheFirstRowOfAnOrderedGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .First()
                )
        );
    }

    [Fact]
    public void RefusesTheLastRowOfAnOrderedGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue)).Last()
                )
        );
    }

    [Fact]
    public void ReadsTheFirstValueOrDefaultOfFilteredOrderedGroups()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, long?, long?>(
                    x.Key,
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => (long?)long.Parse(row.Cells[ValueId].Value.TextValue))
                        .FirstOrDefault(),
                    x.Where(row => row.Cells[Text].Value.TextValue == "absent")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => (long?)long.Parse(row.Cells[ValueId].Value.TextValue))
                        .FirstOrDefault()
                ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfNullableNumbers()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, int?, int?>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[Small].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Small].Value.TextValue),
                }.Max(),
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[Small].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Small].Value.TextValue),
                    3,
                }.Min()
            ))
        );
    }

    [Fact]
    public void ReadsTheGreatestAndLeastOfDatetimesAndOrdinalTexts()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateTime?, string?>(
                x.Cells[ValueId].Value.TextValue,
                new[]
                {
                    string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                        ? null
                        : (DateTime?)
                            DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    new DateTime(2000, 1, 1),
                }.Min(),
                new[] { x.Cells[Text].Value.TextValue, "Z", "plain!" }.Max(
                    StringComparer.Ordinal
                )
            ))
        );
    }

    [Theory]
    [InlineData(NormalizationForm.FormC)]
    [InlineData(NormalizationForm.FormD)]
    [InlineData(NormalizationForm.FormKC)]
    [InlineData(NormalizationForm.FormKD)]
    public void NormalizesTheTextsOfCells(NormalizationForm form)
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, bool>(
                x.Cells[Text].Value.TextValue.Normalize(form),
                x.Cells[Text].Value.TextValue.Normalize(),
                x.Cells[Text].Value.TextValue.Normalize(form) == x.Cells[Text].Value.TextValue
            ))
        );
    }
}
