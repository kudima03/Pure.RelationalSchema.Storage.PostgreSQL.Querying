using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Expressions over the elements of in-memory sequences written as <c>VALUES</c>:
/// text concatenated with NULL, coalesced and compared, element operators and
/// grouping over them, distinct tuples and set operations over wide and nested
/// tuples; each returns in one statement what LINQ to Objects returns over the
/// enumerated rows.
/// </summary>
public sealed record InMemorySequenceExpressionTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] Labels =
    [
        (1, "one"),
        (2, null),
        (4, ""),
        (91, "Ünicøde"),
        (91, null),
        (500, "unmatched"),
    ];

    private readonly ExecutionFixture _fixture;

    public InMemorySequenceExpressionTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Moment => _fixture.Moment;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ConcatenatesCellTextWithInMemoryNulls()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Labels,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) => row.Cells[Text].Value.TextValue + "/" + label.Label
            )
        );
    }

    [Fact]
    public void ConcatenatesInMemoryNullsWithNullableCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                x => Labels,
                (row, label) =>
                    label.Label
                    + (
                        string.IsNullOrEmpty(row.Cells[ChildLabel].Value.TextValue)
                            ? null
                            : row.Cells[ChildLabel].Value.TextValue
                    )
            )
        );
    }

    [Fact]
    public void CoalescesInMemoryNulls()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Labels,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<string, string, bool, bool>(
                        row.Cells[ValueId].Value.TextValue,
                        label.Label ?? "none",
                        label.Label == null,
                        string.IsNullOrEmpty(label.Label)
                    )
            )
        );
    }

    [Fact]
    public void ComparesInMemoryTextWithCellsOrdinally()
    {
        string[] bounds = ["", "a", "plain", "Z", "😀"];

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                x => bounds,
                (row, bound) =>
                    new ValueTuple<string, string, bool, bool>(
                        row.Cells[ChildKey].Value.TextValue,
                        bound,
                        string.CompareOrdinal(row.Cells[ChildKey].Value.TextValue, bound)
                            < 0,
                        row.Cells[ChildKey].Value.TextValue == bound
                    )
            )
        );
    }

    [Fact]
    public void ChangesTheCaseOfInMemoryText()
    {
        string[] texts = ["Ünicøde", "straße", "MiXeD", "ÀÉÎ", ""];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(
                    x => texts,
                    (row, text) =>
                        new ValueTuple<string, string>(
                            text.ToUpperInvariant(),
                            text.ToLowerInvariant()
                        )
                )
        );
    }

    [Fact]
    public void KeepsDifferentlyNormalizedTextApart()
    {
        string[] texts = ["é", "é", "é"];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .Union(texts.AsQueryable())
        );
    }

    [Fact]
    public void ProjectsTheFirstOfAnInMemoryQueryAboveTheRowOrTheDefault()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Where(y => y > long.Parse(x.Cells[ValueId].Value.TextValue) * 10)
                    .OrderBy(y => y)
                    .FirstOrDefault()
            ))
        );
    }

    [Fact]
    public void ProjectsTheNullableFirstOfAnInMemoryQueryAboveTheRow()
    {
        long?[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long?>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Where(y => y > long.Parse(x.Cells[ValueId].Value.TextValue) * 10)
                    .OrderBy(y => y)
                    .FirstOrDefault()
            ))
        );
    }

    [Fact]
    public void ProjectsTheFirstLabelOfAnInMemoryQueryOrNull()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string?>(
                x.Cells[ValueId].Value.TextValue,
                Labels
                    .AsQueryable()
                    .Where(y => y.Id == long.Parse(x.Cells[ValueId].Value.TextValue))
                    .OrderBy(y => y.Label)
                    .Select(y => y.Label)
                    .FirstOrDefault()
            ))
        );
    }

    [Fact]
    public void FiltersByMembershipInTheFirstElementsOfAnInMemoryQuery()
    {
        long[] ids = [91, 4, 1, 3, 2];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .OrderBy(y => y)
                        .Skip(1)
                        .Take(2)
                        .Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByAnyElementOfAnInMemoryQuery()
    {
        long[] ids = [7, 8];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable().Any() && x.Cells[Text].Value.TextValue == "plain"
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void GroupsRowsJoinedToAnInMemorySequenceByItsLabel()
    {
        (long Id, string Group)[] groups =
        [
            (1, "low"),
            (2, "low"),
            (3, "low"),
            (4, "mid"),
            (91, "high"),
            (92, "high"),
            (500, "none"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    groups,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, group) => new { Row = row, group.Group }
                )
                .GroupBy(x => x.Group)
                .Select(x => new ValueTuple<string, int, long>(
                    x.Key,
                    x.Count(),
                    x.Sum(y => long.Parse(y.Row.Cells[ValueId].Value.TextValue))
                ))
        );
    }

    [Fact]
    public void GroupsAnInMemorySequenceCrossJoinedToRowsByANullableKey()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(x => Labels, (row, label) => label)
                .GroupBy(x => x.Label)
                .Select(x => new ValueTuple<string?, int, long>(
                    x.Key,
                    x.Count(),
                    x.Max(y => y.Id)
                ))
        );
    }

    [Fact]
    public void ReadsDistinctTuplesOfAnInMemorySequence()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                    x => Labels,
                    (row, label) => new ValueTuple<long, string?>(label.Id, label.Label)
                )
                .Distinct()
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    public void CombinesWideTuplesOfRowsWithInMemoryWideTuples(string operation)
    {
        ValueTuple<
            long,
            string,
            int,
            int,
            int,
            int,
            int,
            ValueTuple<string?, bool>
        >[] items =
        [
            new ValueTuple<
                long,
                string,
                int,
                int,
                int,
                int,
                int,
                ValueTuple<string?, bool>
            >(1, "plain", 1, 2, 3, 4, 5, new ValueTuple<string?, bool>("plain", true)),
            new ValueTuple<
                long,
                string,
                int,
                int,
                int,
                int,
                int,
                ValueTuple<string?, bool>
            >(2, "", 1, 2, 3, 4, 5, new ValueTuple<string?, bool>(null, true)),
            new ValueTuple<
                long,
                string,
                int,
                int,
                int,
                int,
                int,
                ValueTuple<string?, bool>
            >(600, "x", 1, 2, 3, 4, 5, new ValueTuple<string?, bool>(null, false)),
        ];

        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x => new ValueTuple<
                    long,
                    string,
                    int,
                    int,
                    int,
                    int,
                    int,
                    ValueTuple<string?, bool>
                >(
                    long.Parse(x.Cells[ValueId].Value.TextValue),
                    x.Cells[Text].Value.TextValue,
                    1,
                    2,
                    3,
                    4,
                    5,
                    new ValueTuple<string?, bool>(
                        string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                            ? null
                            : x.Cells[Text].Value.TextValue,
                        true
                    )
                )),
                items.AsQueryable()
            )
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    public void CombinesNestedTuplesOfRowsWithInMemoryNestedTuples(string operation)
    {
        ((long Id, string? Text) Key, bool Flag)[] items =
        [
            ((1, "plain"), true),
            ((2, null), true),
            ((3, "x"), false),
        ];

        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x => new ValueTuple<ValueTuple<long, string?>, bool>(
                    new ValueTuple<long, string?>(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                            ? null
                            : x.Cells[Text].Value.TextValue
                    ),
                    true
                )),
                items.AsQueryable()
            )
        );
    }

    [Fact]
    public void JoinsADatetimeColumnToInMemoryDatetimesOfUtcKind()
    {
        DateTime[] moments =
        [
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Utc),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                moments,
                x =>
                    string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                        ? null
                        : DateTime.Parse(
                            x.Cells[Moment].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ),
                x => (DateTime?)x,
                (row, moment) => row.Cells[ValueId].Value.TextValue
            )
        );
    }

    [Fact]
    public void ComparesStoredDatetimesWithInMemoryBounds()
    {
        DateTime[] bounds =
        [
            new DateTime(1999, 12, 31, 23, 59, 59).AddTicks(9000000),
            new DateTime(2000, 1, 1),
            new DateTime(9999, 12, 31, 23, 59, 59).AddTicks(9999990),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.SelectMany(
                    x => bounds,
                    (row, bound) => new { Row = row, Bound = bound }
                )
                .Where(x =>
                    !string.IsNullOrEmpty(x.Row.Cells[Moment].Value.TextValue)
                    && DateTime.Parse(
                        x.Row.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ) >= x.Bound
                )
                .Select(x => new ValueTuple<string, DateTime>(
                    x.Row.Cells[ValueId].Value.TextValue,
                    x.Bound
                ))
        );
    }

    [Fact]
    public void ComputesWithInMemoryTemporalValues()
    {
        (DateOnly Day, TimeSpan Span)[] items =
        [
            (new DateOnly(2024, 2, 28), TimeSpan.FromDays(1)),
            (new DateOnly(2023, 12, 31), TimeSpan.FromHours(-25)),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(
                    x => items,
                    (row, item) =>
                        new ValueTuple<DateOnly, int, int, double>(
                            item.Day.AddDays(1),
                            item.Day.Year,
                            item.Day.DayOfYear,
                            item.Span.TotalHours
                        )
                )
        );
    }

    [Fact]
    public void FiltersRowsJoinedToAnInMemorySequenceByBoth()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Join(
                    Labels,
                    x => long.Parse(x.Cells[ChildId].Value.TextValue),
                    x => x.Id,
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x =>
                    x.Label.Label != null || x.Row.Cells[ChildKey].Value.TextValue == "a"
                )
                .Select(x => new ValueTuple<string, string?, string>(
                    x.Row.Cells[ChildId].Value.TextValue,
                    x.Label.Label,
                    x.Row.Cells[ChildKey].Value.TextValue
                ))
        );
    }

    private static IQueryable<T> Combined<T>(
        string operation,
        IQueryable<T> first,
        IQueryable<T> second
    )
    {
        return operation switch
        {
            "Union" => first.Union(second),
            "Intersect" => first.Intersect(second),
            _ => first.Except(second),
        };
    }
}
