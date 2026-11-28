using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The shapes of the translator's defects of in-memory <c>VALUES</c>, of
/// <c>GREATEST</c> / <c>LEAST</c> and ordinal texts, and of <c>string.Join</c> and
/// the elements of groups (translator issue #128) over stored tables: the first
/// distinct values and groups of a sequence, a one-element query tested for
/// existence, the extremes of arrays of defaults and of captured arrays, the ordinal
/// extremes of a group, aggregates of subqueries that read only outer values, a
/// separator that aggregates its group, a captured query joined, the last of the
/// distinct values of a group and the order of the source before <c>GroupBy</c>;
/// each returns in one statement what it returns over the enumerated rows.
/// </summary>
public sealed record TranslatorDefectValueTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public TranslatorDefectValueTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private BuiltValueComparison Comparison =>
        new BuiltValueComparison(_fixture, _output);

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void TakesTheFirstDistinctValuesOfAnInMemoryRootInSequenceOrder()
    {
        int[] numbers = [3, 1, 3, 2];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<int>(
                numbers
                    .AsQueryable()
                    .Where(x => db.Values.Any())
                    .Distinct()
                    .Take(2)
                    .Expression
            )
        );
    }

    [Fact]
    public void SkipsTheFirstDistinctValuesOfAnInMemorySequenceJoinedToARow()
    {
        int[] numbers = [3, 1, 3, 2];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => numbers, (row, number) => number)
                .Distinct()
                .Skip(2)
        );
    }

    [Fact]
    public void TakesTheFirstGroupsOfAnInMemoryRootInSequenceOrder()
    {
        int[] numbers = [3, 1, 3, 2];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<ValueTuple<int, int>>(
                numbers
                    .AsQueryable()
                    .Where(x => db.Values.Any())
                    .GroupBy(x => x)
                    .Select(x => new ValueTuple<int, int>(x.Key, x.Count()))
                    .Take(2)
                    .Expression
            )
        );
    }

    [Fact]
    public void FiltersByTheExistenceOfAMatchInAnInMemoryQueryOfOneElement()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .Any(y => y == long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[Text].Value.TextValue)
        );
    }

    [Fact]
    public void TakesTheExtremesOfArraysOfDefaultsSizedByACell()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, int, int?, long?>(
                    x,
                    new int[x].Max(),
                    new int?[x].Min(),
                    new long?[x].Max()
                ))
        );
    }

    [Fact]
    public void FoldsTheExtremesOfACapturedArray()
    {
        int[] values = [3, 9, 4];

        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => (int)long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => new ValueTuple<int, int, int>(
                    x,
                    values.Max(),
                    new[] { x, values.Min() }.Max()
                ))
        );
    }

    [Fact]
    public void TakesTheOrdinalExtremesOfAGroupInUtf16Order()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x =>
                    x.Cells[ValueId].Value.TextValue == "1"
                    || x.Cells[ValueId].Value.TextValue == "3"
                )
                .GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, string?, string?>(
                    x.Key,
                    x.Select(row =>
                            row.Cells[ValueId].Value.TextValue == "3"
                                ? row.Cells[Text].Value.TextValue
                                : "Ünicøde Ａ"
                        )
                        .Max(StringComparer.Ordinal),
                    x.Select(row =>
                            row.Cells[ValueId].Value.TextValue == "3"
                                ? row.Cells[Text].Value.TextValue
                                : "Ünicøde Ａ"
                        )
                        .Min(StringComparer.Ordinal)
                ))
        );
    }

    [Fact]
    public void JoinsTheOuterValuesOfASubquery()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string>(
                parent.Cells[ParentId].Value.TextValue,
                string.Join(
                    ",",
                    db.Children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Select(child => parent.Cells[ParentId].Value.TextValue)
                )
            ))
        );
    }

    [Fact]
    public void JoinsTheOuterValuesOfGroupJoinGroups()
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
                            "/",
                            children.Select(child =>
                                parent.Cells[ParentKey].Value.TextValue
                            )
                        )
                    )
            )
        );
    }

    [Fact]
    public void TakesTheOrdinalMaximumOfTheOuterValuesOfASubquery()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string?>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Where(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                    .Select(child => parent.Cells[ParentKey].Value.TextValue)
                    .Max(StringComparer.Ordinal)
            ))
        );
    }

    [Fact]
    public void SumsTheOuterValuesOfASubquery()
    {
        Comparison.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, long>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Where(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                    .Sum(child => long.Parse(parent.Cells[ParentId].Value.TextValue))
            ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfAGroupWithASeparatorThatAggregatesIt()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildNumber].Value.TextValue)
                .Select(x => new ValueTuple<string, string>(
                    x.Key,
                    string.Join(
                        x.Count().ToString(),
                        x.OrderBy(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                            .Select(row => row.Cells[ChildLabel].Value.TextValue)
                    )
                ))
        );
    }

    [Fact]
    public void JoinsACapturedQueryOfStoredTexts()
    {
        Comparison.AssertSameElements(db =>
        {
            IQueryable<string> labels = db
                .Children.OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Select(x => x.Cells[ChildLabel].Value.TextValue);
            return db.Parents.Select(x => new ValueTuple<string, string>(
                x.Cells[ParentId].Value.TextValue,
                string.Join(",", labels)
            ));
        });
    }

    [Fact]
    public void ReadsTheLastOfTheDistinctValuesOfAnOrderedGroup()
    {
        Comparison.AssertSameElements(db =>
            db.Children.GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderBy(row => long.Parse(row.Cells[ChildId].Value.TextValue))
                        .Select(row => row.Cells[ChildNumber].Value.TextValue)
                        .Distinct()
                        .Last(),
                    x.OrderByDescending(row =>
                            long.Parse(row.Cells[ChildId].Value.TextValue)
                        )
                        .Select(row => row.Cells[ChildNumber].Value.TextValue)
                        .Distinct()
                        .Last()
                ))
        );
    }

    [Fact]
    public void JoinsTheTextsOfGroupsOfAnOrderedSourceInTheOrderOfTheSource()
    {
        Comparison.AssertSameElements(db =>
            db.Children.OrderByDescending(x =>
                    long.Parse(x.Cells[ChildId].Value.TextValue)
                )
                .GroupBy(x => x.Cells[ChildKey].Value.TextValue)
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
    public void BreaksTiesOfGroupsOfAnOrderedSourceInTheOrderOfTheSource()
    {
        Comparison.AssertSameElements(db =>
            db.Children.OrderByDescending(x =>
                    long.Parse(x.Cells[ChildId].Value.TextValue)
                )
                .GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => new ValueTuple<string, string, string>(
                    x.Key,
                    x.OrderBy(
                            row => row.Cells[ChildKey].Value.TextValue,
                            StringComparer.Ordinal
                        )
                        .Select(row => row.Cells[ChildId].Value.TextValue)
                        .First(),
                    x.OrderBy(
                            row => row.Cells[ChildKey].Value.TextValue,
                            StringComparer.Ordinal
                        )
                        .Select(row => row.Cells[ChildId].Value.TextValue)
                        .Last()
                ))
        );
    }
}
