using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// <c>First</c>, <c>FirstOrDefault</c>, <c>Last</c> and <c>LastOrDefault</c> of a value
/// of a group sorted with <c>OrderBy</c> and <c>ThenBy</c> return in one statement
/// what they return over the enumerated rows: with keys that tie (LINQ sorts
/// stably, so the first and last of tied elements are those of the source order),
/// nullable keys ordered with their NULLs first ascending and last descending,
/// values that are NULL, filters that leave no element and element selectors.
/// </summary>
public sealed record OrderedGroupValueTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public OrderedGroupValueTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Moment => _fixture.Moment;

    private IColumn Code => _fixture.Code;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private BuiltValueComparison Comparison => new BuiltValueComparison(_fixture, _output);

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsTheFirstAndLastValuesOfGroupsInTheOrderOfUniqueKeys()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string, string, string>(
                    x.Key,
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last(),
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Text].Value.TextValue)
                        .Last(),
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Moment].Value.TextValue)
                        .FirstOrDefault()!
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #134): First of a group sorted by keys that tie takes any of the tied"
            + " elements (ARRAY_AGG(... ORDER BY key) does not sort stably), where .NET"
            + " takes the first in the order of the source."
    )]
    public void ReadsTheFirstValueOfGroupsWhoseKeysTie()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderBy(row => row.Cells[Small].Value.TextValue.Length)
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(),
                    x.OrderByDescending(row => row.Cells[Text].Value.TextValue.Length)
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First()
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #134): Last of a group sorted by keys that tie is the first element of"
            + " ARRAY_AGG in the inverted order, any of the tied elements (even a stable"
            + " sort would give the first of them), where .NET takes the last in the order"
            + " of the source."
    )]
    public void ReadsTheLastValueOfGroupsWhoseKeysTie()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderByDescending(row => row.Cells[Small].Value.TextValue.Length)
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last(),
                    x.OrderBy(row => row.Cells[Text].Value.TextValue.Length)
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last()
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstAndLastValuesInTheOrderOfANullableKeyThenAnother()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string, string, string>(
                    x.Key,
                    x.OrderBy(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(),
                    x.OrderBy(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last(),
                    x.OrderByDescending(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(),
                    x.OrderByDescending(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .ThenByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last()
                ))
        );
    }

    [Fact]
    public void ReadsNullValuesFirstAndLastInGroups()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, int?, int?, string?>(
                    x.Key,
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .First(),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                        )
                        .LastOrDefault(),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            string.IsNullOrEmpty(row.Cells[Flag].Value.TextValue)
                                ? null
                                : row.Cells[Flag].Value.TextValue
                        )
                        .Last()
                ))
        );
    }

    [Fact]
    public void ReadsTheDefaultsOfFilteredGroupsWhereNoElementIsLeft()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, long, string?, int, bool>(
                    x.Key,
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .FirstOrDefault(id => id > 5),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Text].Value.TextValue)
                        .LastOrDefault(text => text == "plain"),
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => (int)long.Parse(row.Cells[ValueId].Value.TextValue))
                        .LastOrDefault(id => id > 1000),
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Text].Value.TextValue == "plain")
                        .FirstOrDefault(plain => plain)
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstValuesOfGroupsFilteredBeforeTheyAreOrdered()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string?, DateTime?>(
                    x.Key,
                    x.Where(row => row.Cells[Small].Value.TextValue == "7")
                        .OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .FirstOrDefault(),
                    x.Where(row => !string.IsNullOrEmpty(row.Cells[Moment].Value.TextValue))
                        .OrderBy(row =>
                            DateTime.Parse(
                                row.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                        )
                        .Select(row =>
                            (DateTime?)
                                DateTime.Parse(
                                    row.Cells[Moment].Value.TextValue,
                                    CultureInfo.InvariantCulture
                                )
                        )
                        .LastOrDefault()
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstAndLastElementsOfGroupsWithAnElementSelector()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(
                    x => x.Cells[Flag].Value.TextValue,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue)
                )
                .Select(x => new ValueTuple<string, long, long, long>(
                    x.Key,
                    x.OrderBy(id => id).First(),
                    x.OrderBy(id => id).Last(),
                    x.OrderByDescending(id => id % 10).ThenBy(id => id).First()
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstValueInTheOrdinalOrderOfTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderBy(row => row.Cells[ChildLabel].Value.TextValue, StringComparer.Ordinal)
                        .Select(row => row.Cells[ChildId].Value.TextValue)
                        .First(),
                    x.OrderBy(row => row.Cells[ChildLabel].Value.TextValue, StringComparer.Ordinal)
                        .Select(row => row.Cells[ChildId].Value.TextValue)
                        .Last()
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstValuesOfGroupsOfComputedValues()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderByDescending(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                        .Select(row =>
                            row.Cells[ChildLabel].Value.TextValue
                            + "#"
                            + row.Cells[ChildKey].Value.TextValue
                        )
                        .First(),
                    x.OrderBy(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                        .Select(row =>
                            string.Join(
                                "|",
                                new[]
                                {
                                    row.Cells[ChildLabel].Value.TextValue,
                                    row.Cells[ChildId].Value.TextValue,
                                }
                            )
                        )
                        .Last()
                ))
        );
    }

    [Fact]
    public void OrdersGroupsByTheirFirstValues()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, long>(
                    x.Key,
                    x.OrderByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .First()
                ))
                .OrderBy(x => x.Item2)
        );
    }

    [Fact]
    public void FiltersGroupsByTheirLastValues()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Where(x =>
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[Text].Value.TextValue)
                        .Last() == "plain"
                )
                .Select(x => x.Key)
        );
    }

    [Fact]
    public void RefusesTheFirstValueOfAFilteredGroupWithoutADefault()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(id => id != "2")
                )
        );
    }

    [Fact]
    public void RefusesTheLastValueOfAnUnorderedGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => x.Select(row => row.Cells[ValueId].Value.TextValue).Last())
        );
    }

    [Fact]
    public void RefusesAGroupOrderedTwice()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    x.OrderBy(row => row.Cells[Text].Value.TextValue.Length)
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First()
                )
        );
    }

    // FirstOrDefault(defaultValue) of a group is not translated.
    [Fact]
    public void RefusesTheFirstValueOrAGivenDefaultOfFilteredGroups()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, long>(
                    x.Key,
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .FirstOrDefault(-1L)
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstValuesOfOrderedGroupJoinGroups()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.GroupJoin(
                db.Children,
                parent => parent.Cells[ParentKey].Value.TextValue,
                child => child.Cells[ChildKey].Value.TextValue,
                (parent, children) =>
                    new ValueTuple<string, string?>(
                        parent.Cells[ParentId].Value.TextValue,
                        children
                            .OrderByDescending(child =>
                                long.Parse(child.Cells[ChildId].Value.TextValue)
                            )
                            .Select(child => child.Cells[ChildLabel].Value.TextValue)
                            .FirstOrDefault()
                    )
            )
        );
    }

    [Fact]
    public void ReadsTheDefaultsOfValueTypesOfFilteredGroupsWhereNoElementIsLeft()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, DateTime, Guid, decimal, bool>(
                    x.Key,
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            DateTime.Parse(
                                row.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                        )
                        .FirstOrDefault(),
                    x.Where(row => row.Cells[Small].Value.TextValue == "65535")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => Guid.Parse(row.Cells[Code].Value.TextValue))
                        .LastOrDefault(),
                    x.Where(row => row.Cells[Text].Value.TextValue == " padded ")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => (decimal)int.Parse(row.Cells[Number].Value.TextValue))
                        .FirstOrDefault(),
                    x.Where(row => row.Cells[Text].Value.TextValue == "absent")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => bool.Parse(row.Cells[Flag].Value.TextValue))
                        .LastOrDefault()
                ))
        );
    }

    [Fact]
    public void ReadsTheDefaultDateAndTimeOfFilteredGroupsWhereNoElementIsLeft()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, DateOnly, TimeOnly>(
                    x.Key,
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            new DateOnly(
                                2000,
                                1,
                                ((int)long.Parse(row.Cells[ValueId].Value.TextValue) % 28) + 1
                            )
                        )
                        .FirstOrDefault(),
                    x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                        .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row =>
                            new TimeOnly(
                                (int)long.Parse(row.Cells[ValueId].Value.TextValue) % 24,
                                0
                            )
                        )
                        .LastOrDefault()
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstValuesOfGroupsInTheOrderOfBuiltDates()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderBy(row =>
                            new DateOnly(
                                2000,
                                ((int)long.Parse(row.Cells[ValueId].Value.TextValue) % 12) + 1,
                                1
                            )
                        )
                        .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .First(),
                    x.OrderBy(row =>
                            new[]
                            {
                                long.Parse(row.Cells[ValueId].Value.TextValue),
                                10L,
                            }.Max()
                        )
                        .ThenByDescending(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last()
                ))
        );
    }

    [Fact]
    public void ReadsTheLastValueInTheDescendingOrdinalOrderOfTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderByDescending(
                            row => row.Cells[Text].Value.TextValue,
                            StringComparer.Ordinal
                        )
                        .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .Last(),
                    x.OrderBy(row => row.Cells[Text].Value.TextValue, StringComparer.Ordinal)
                        .ThenByDescending(
                            row => row.Cells[Moment].Value.TextValue,
                            StringComparer.Ordinal
                        )
                        .Select(row => row.Cells[Moment].Value.TextValue)
                        .First()
                ))
        );
    }

    // The elements of a group are not paged in SQL.
    [Fact]
    public void RefusesTheFirstValueOfAPagedGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string?>(
                    x.Key,
                    x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                        .Skip(1)
                        .Select(row => row.Cells[ValueId].Value.TextValue)
                        .FirstOrDefault()
                ))
        );
    }
}
