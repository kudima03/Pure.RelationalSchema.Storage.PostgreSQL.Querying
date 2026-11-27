using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// <c>string.Join</c> of the texts of stored cells — over an array, a group, a
/// subquery and a <c>GroupJoin</c> group — returns in one statement what it returns
/// over the enumerated rows: with separators that are empty, null, of several
/// characters, holding quotes, a character or read from a cell; a null value
/// joined as the empty text; an empty sequence joined as the empty text, not
/// <c>null</c>; and the values in the order of the sequence. Values that are not
/// strings, which PostgreSQL formats differently, are refused.
/// </summary>
public sealed record StringJoinTests : IClassFixture<ExecutionFixture>
{
    private static readonly string?[] Separators =
    [
        "",
        null,
        "'",
        "''",
        "\\",
        "\"",
        "%",
        "_",
        "', '",
        "\\'",
        "$$",
        " | ",
        "Ünicøde 😀",
    ];

    private static readonly string?[] Words = ["a", null, "", "'b'"];

    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public StringJoinTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private IColumn EntryId => _fixture.EntryId;

    private IColumn EntryKey => _fixture.EntryKey;

    private BuiltValueComparison Comparison => new BuiltValueComparison(_fixture, _output);

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void JoinsAnArrayWithSeparatorsOfInMemoryTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.SelectMany(
                x => Separators,
                (row, separator) =>
                    new ValueTuple<string, string?, string>(
                        row.Cells[ValueId].Value.TextValue,
                        separator,
                        string.Join(
                            separator,
                            new[]
                            {
                                row.Cells[Text].Value.TextValue,
                                separator,
                                row.Cells[ValueId].Value.TextValue,
                            }
                        )
                    )
            )
        );
    }

    [Fact]
    public void JoinsAnArrayWithConstantSeparators()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, string, string, string>(
                string.Join(
                    "",
                    new[] { x.Cells[Text].Value.TextValue, x.Cells[ValueId].Value.TextValue }
                ),
                string.Join(
                    null,
                    new[] { x.Cells[Text].Value.TextValue, x.Cells[ValueId].Value.TextValue }
                ),
                string.Join(
                    "', E'\\n",
                    new[] { x.Cells[Text].Value.TextValue, x.Cells[ValueId].Value.TextValue }
                ),
                string.Join(
                    ',',
                    new[] { x.Cells[Text].Value.TextValue, x.Cells[ValueId].Value.TextValue }
                ),
                string.Join(
                    '\'',
                    new[] { x.Cells[ValueId].Value.TextValue, x.Cells[Text].Value.TextValue }
                )
            ))
        );
    }

    [Fact]
    public void JoinsWithTheSeparatorOfACell()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, string>(
                x.Cells[ValueId].Value.TextValue,
                string.Join(
                    x.Cells[Text].Value.TextValue,
                    new[] { x.Cells[ValueId].Value.TextValue, x.Cells[Small].Value.TextValue }
                ),
                string.Join(
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : x.Cells[Flag].Value.TextValue,
                    new[] { x.Cells[ValueId].Value.TextValue, x.Cells[Text].Value.TextValue }
                )
            ))
        );
    }

    [Fact]
    public void JoinsWithACharacterSeparatorChosenByACell()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x =>
                string.Join(
                    x.Cells[Flag].Value.TextValue == "True" ? ';' : '\'',
                    new[] { x.Cells[ValueId].Value.TextValue, x.Cells[Text].Value.TextValue }
                )
            )
        );
    }

    [Fact]
    public void JoinsNullValuesOfAnArrayAsEmptyTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, string, string>(
                string.Join(
                    "-",
                    new string?[] { null, x.Cells[Text].Value.TextValue, null }
                ),
                string.Join(
                    "-",
                    new[]
                    {
                        string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                            ? null
                            : x.Cells[Number].Value.TextValue,
                        string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            ? null
                            : x.Cells[Flag].Value.TextValue,
                    }
                ),
                string.Join("-", new string?[] { null, null }),
                string.Join("-", new[] { x.Cells[Text].Value.TextValue })
            ))
        );
    }

    [Fact]
    public void JoinsAnInMemoryArrayWithTheSeparatorOfACell()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string>(
                x.Cells[ValueId].Value.TextValue,
                string.Join(x.Cells[Text].Value.TextValue, Words)
            ))
        );
    }

    [Fact]
    public void FiltersByAJoinedText()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x =>
                    string.Join(
                        ",",
                        new[] { x.Cells[Text].Value.TextValue, x.Cells[Flag].Value.TextValue }
                    ) == "plain,True"
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void GroupsByAJoinedText()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x =>
                    string.Join(
                        "|",
                        new[] { x.Cells[Text].Value.TextValue, x.Cells[Small].Value.TextValue }
                    )
                )
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
    }

    [Fact]
    public void JoinsTheTextsOfGroupsInTheOrderOfTheirRows()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.Select(row => row.Cells[ChildLabel].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfGroupsInTheOrderOfTwoKeys()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row => row.Cells[Text].Value.TextValue, StringComparer.Ordinal)
                            .ThenByDescending(row =>
                                long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    ),
                    string.Join(
                        ";",
                        x.OrderByDescending(row =>
                                string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                    ? null
                                    : (int?)int.Parse(row.Cells[Number].Value.TextValue)
                            )
                            .ThenBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfGroupsInTheOrderOfANullableKey()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row =>
                                string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                    ? null
                                    : (long?)long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    ),
                    string.Join(
                        ",",
                        x.OrderByDescending(row =>
                                string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                    ? null
                                    : (long?)long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheNullValuesOfAGroupAsEmptyTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row =>
                                string.IsNullOrEmpty(row.Cells[Flag].Value.TextValue)
                                    ? null
                                    : row.Cells[Flag].Value.TextValue
                            )
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheFilteredTextsOfGroupsAsEmptyTextsWhereNoneIsLeft()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.Where(row => row.Cells[Text].Value.TextValue == "plain")
                            .OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    ),
                    string.Join(
                        ",",
                        x.Where(row => row.Cells[Text].Value.TextValue == "absent")
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheElementsOfGroupsWithAnElementSelector()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(
                    x => x.Cells[Flag].Value.TextValue,
                    x => x.Cells[Text].Value.TextValue
                )
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        "/",
                        x.OrderByDescending(text => text, StringComparer.Ordinal)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheDistinctTextsOfGroupsInTheirOwnOrder()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.Select(row => row.Cells[ChildLabel].Value.TextValue)
                            .Distinct()
                            .OrderBy(label => label, StringComparer.Ordinal)
                    ),
                    string.Join(
                        ",",
                        x.Select(row => row.Cells[ChildLabel].Value.TextValue)
                            .Distinct()
                            .OrderByDescending(label => label, StringComparer.Ordinal)
                    )
                ))
        );
    }

    [Fact(
        Skip = "PENDING: string.Join of the distinct values of an unordered group joins"
            + " them sorted by STRING_AGG(DISTINCT ...), not in the order of their first"
            + " occurrence as .NET does."
    )]
    public void JoinsTheDistinctTextsOfGroupsInTheOrderOfTheirFirstOccurrence()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.Select(row => row.Cells[ChildLabel].Value.TextValue).Distinct()
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfAnOrderedSubqueryInItsOrder()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            != parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderBy(child => child.Cells[ChildLabel].Value.TextValue.Length)
                        .ThenByDescending(child =>
                            long.Parse(child.Cells[ChildId].Value.TextValue)
                        )
                        .Select(child =>
                            child.Cells[ChildLabel].Value.TextValue
                            + child.Cells[ChildId].Value.TextValue
                        )
                ),
                string.Join(
                    ",",
                    db.Entries.Where(entry =>
                            entry.Cells[EntryKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderByDescending(entry =>
                            long.Parse(entry.Cells[EntryId].Value.TextValue)
                        )
                        .Select(entry => entry.Cells[EntryId].Value.TextValue)
                )
            ))
        );
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 10)]
    [InlineData(10, 1)]
    public void JoinsTheTextsOfAPagedSubqueryInItsOrder(int skip, int take)
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.OrderByDescending(child =>
                            long.Parse(child.Cells[ChildId].Value.TextValue)
                        )
                        .Select(child => child.Cells[ChildLabel].Value.TextValue)
                        .Skip(skip)
                        .Take(take)
                ),
                string.Join(
                    ",",
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            != parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderBy(child => long.Parse(child.Cells[ChildId].Value.TextValue))
                        .Skip(skip)
                        .Take(take)
                        .Select(child => child.Cells[ChildId].Value.TextValue)
                )
            ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfASubqueryOrderedBeforeItIsFiltered()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.OrderByDescending(child =>
                            long.Parse(child.Cells[ChildId].Value.TextValue)
                        )
                        .Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Select(child => child.Cells[ChildLabel].Value.TextValue)
                )
            ))
        );
    }

    [Fact]
    public void JoinsTheDistinctTextsOfAnOrderedSubquery()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.Select(child => child.Cells[ChildKey].Value.TextValue)
                        .Distinct()
                        .OrderByDescending(key => key, StringComparer.Ordinal)
                )
            ))
        );
    }

    [Fact(
        Skip = "PENDING: string.Join of the distinct values of an unordered subquery joins"
            + " them in another order than that of their first occurrence."
    )]
    public void JoinsTheDistinctTextsOfASubqueryInTheOrderOfTheirFirstOccurrence()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.Select(child => child.Cells[ChildLabel].Value.TextValue)
                        .Distinct()
                )
            ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfGroupJoinGroups()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.GroupJoin(
                db.Children,
                parent => parent.Cells[ParentKey].Value.TextValue,
                child => child.Cells[ChildKey].Value.TextValue,
                (parent, children) =>
                    new ValueTuple<string, string, string>(
                        parent.Cells[ParentId].Value.TextValue,
                        string.Join(
                            "/",
                            children
                                .OrderByDescending(child =>
                                    long.Parse(child.Cells[ChildId].Value.TextValue)
                                )
                                .Select(child => child.Cells[ChildLabel].Value.TextValue)
                        ),
                        string.Join(
                            "/",
                            children.Select(child => child.Cells[ChildId].Value.TextValue)
                        )
                    )
            )
        );
    }

    [Fact]
    public void JoinsTheTextsOfAWholeTable()
    {
        Comparison.AssertSameValue(db =>
            string.Join(
                ",",
                db.Children.OrderBy(child => long.Parse(child.Cells[ChildId].Value.TextValue))
                    .Select(child => child.Cells[ChildLabel].Value.TextValue)
            )
        );
    }

    [Fact]
    public void RefusesJoiningNumbers()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                )
        );
    }

    [Fact]
    public void RefusesJoiningObjects()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x =>
                string.Join(
                    ",",
                    new object[]
                    {
                        x.Cells[Text].Value.TextValue,
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        x.Cells[Flag].Value.TextValue == "true",
                    }
                )
            )
        );
    }

    [Fact]
    public void RefusesJoiningARangeOfAnArray()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x =>
                string.Join(
                    ",",
                    new[]
                    {
                        x.Cells[Text].Value.TextValue,
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Flag].Value.TextValue,
                    },
                    1,
                    1
                )
            )
        );
    }

    [Fact]
    public void JoinsTheValuesOfAGroupThatAreNotNull()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row =>
                                string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                    ? null
                                    : row.Cells[Number].Value.TextValue
                            )
                            .Where(number => number != null)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfAGroupWithItsKeyAsTheSeparator()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    string.Join(
                        x.Key,
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                    )
                )
        );
    }

    [Fact]
    public void JoinsTextsJoinedInTurn()
    {
        Comparison.AssertSameElements(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x =>
                    string.Join(
                        ";",
                        x.OrderByDescending(row =>
                                long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                            .Select(row =>
                                string.Join(
                                    ",",
                                    new[]
                                    {
                                        row.Cells[ValueId].Value.TextValue,
                                        row.Cells[Text].Value.TextValue,
                                    }
                                )
                            )
                    )
                )
        );
    }

    [Fact]
    public void OrdersGroupsByTheirJoinedTexts()
    {
        Enumeration.AssertSameOrder(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x =>
                    string.Join(
                        "",
                        x.OrderBy(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                            .Select(row => row.Cells[ChildLabel].Value.TextValue)
                    )
                )
                .OrderBy(x => x, StringComparer.Ordinal)
        );
    }

    [Fact]
    public void JoinsTheFilteredTextsOfGroupJoinGroups()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.GroupJoin(
                db.Children,
                parent => parent.Cells[ParentKey].Value.TextValue,
                child => child.Cells[ChildKey].Value.TextValue,
                (parent, children) =>
                    new ValueTuple<string, string>(
                        parent.Cells[ParentId].Value.TextValue,
                        string.Join(
                            "",
                            children
                                .Where(child => child.Cells[ChildLabel].Value.TextValue != "y")
                                .OrderBy(child =>
                                    long.Parse(child.Cells[ChildId].Value.TextValue)
                                )
                                .Select(child => child.Cells[ChildLabel].Value.TextValue)
                        )
                    )
            )
        );
    }

    [Fact]
    public void CountsTheGroupsOfJoinedTexts()
    {
        Comparison.AssertSameValue(db =>
            db.Children.GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x =>
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                            .Select(row => row.Cells[ChildNumber].Value.TextValue)
                    )
                )
                .Count(x => x.Contains(','))
        );
    }

    // The elements of a group are not paged in SQL.
    [Fact]
    public void RefusesJoiningTheTextsOfAPagedGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderBy(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                            .Select(row => row.Cells[ValueId].Value.TextValue)
                            .Take(2)
                    )
                ))
        );
    }

    // STRING_AGG(DISTINCT ...) orders by its value only.
    [Fact]
    public void RefusesJoiningTheDistinctTextsOfAGroupSortedBeforehand()
    {
        Enumeration.AssertNotSupported(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        ",",
                        x.OrderByDescending(row =>
                                long.Parse(row.Cells[ChildId].Value.TextValue)
                            )
                            .Select(row => row.Cells[ChildLabel].Value.TextValue)
                            .Distinct()
                    )
                ))
        );
    }

    [Fact]
    public void RefusesJoiningTheDistinctTextsOfAGroupSortedByAnotherKey()
    {
        Enumeration.AssertNotSupported(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x =>
                    string.Join(
                        ",",
                        x.Select(row => row.Cells[ChildLabel].Value.TextValue)
                            .Distinct()
                            .OrderBy(label => label.Length)
                    )
                )
        );
    }
}
