using Pure.RelationalSchema.Abstractions.Column;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

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

    [Fact(
        Skip = "#111 (translator #128): Distinct().Take(n) of VALUES is SELECT DISTINCT "
            + "... LIMIT n, which keeps any n distinct values, where LINQ keeps the "
            + "first occurrences in the order of the sequence (also translator #134)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): Distinct().Skip(n) of VALUES skips any n "
            + "distinct values, where LINQ skips the first occurrences in the order of "
            + "the sequence (also translator #134)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): GroupBy().Take(n) of VALUES is GROUP BY ... "
            + "LIMIT n, which keeps any n groups, where LINQ keeps the groups in the "
            + "order of their first elements (also translator #134)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): a one-element in-memory query is taken for the "
            + "one-row root wherever it is, so Any over it in a filter is refused, where "
            + "a one-element array works (also translator #137)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): Max and Min of new T[n] are GREATEST(n) and "
            + "LEAST(n), the length of the array read as its only element, where the "
            + "array holds n zeros or n nulls."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): C# 14 binds Max and Min of a captured int[] to "
            + "MemoryExtensions over a ReadOnlySpan, which the translator refuses (\"The "
            + "method 'MemoryExtensions.Max' is not supported.\") instead of folding it."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): Max and Min of a group with "
            + "StringComparer.Ordinal are MAX and MIN in the \"C\" collation, which "
            + "compares code points, where .NET compares UTF-16 code units: U+1F600 "
            + "orders after U+FF21 in SQL, before it in .NET (also translator #133)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): STRING_AGG in a subquery whose arguments are "
            + "outer values only is an aggregate of the outer query, which PostgreSQL "
            + "rejects (42803)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): STRING_AGG of a GroupJoin group whose arguments "
            + "are outer values only is an aggregate of the outer query, which "
            + "PostgreSQL rejects (42803)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): MAX in a subquery whose argument is an outer "
            + "value only is an aggregate of the outer query, which PostgreSQL rejects "
            + "(42803)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): SUM in a subquery whose argument is an outer "
            + "value only is an aggregate of the outer query, which PostgreSQL rejects "
            + "(42803)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): a separator that aggregates the group is nested "
            + "in STRING_AGG, which PostgreSQL rejects (42803: aggregate function calls "
            + "cannot be nested)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): Last of the distinct values of an ordered group "
            + "is the value of the last element ((ARRAY_AGG(... ORDER BY ... DESC))[1]), "
            + "not the last of the distinct values."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): string.Join of a group ignores the order of the "
            + "source before GroupBy: STRING_AGG has no ORDER BY, where LINQ keeps the "
            + "elements of a group in the order of the source (also translator #134)."
    )]
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

    [Fact(
        Skip = "#111 (translator #128): First and Last of a group sorted by a key that "
            + "ties ignore the order of the source before GroupBy, where LINQ sorts "
            + "stably in it (also translator #134)."
    )]
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
