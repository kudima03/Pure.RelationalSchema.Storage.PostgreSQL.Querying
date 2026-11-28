using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// In-memory collections tested for the cells of stored rows: <c>Contains</c> of a
/// captured array, list or set (empty, holding NULL, negated, large), and
/// <c>Any</c>, <c>All</c>, <c>Count</c> and the aggregates of an in-memory sequence
/// inside a predicate or a projection; each returns in one statement what LINQ to
/// Objects returns over the enumerated rows, whichever way the collection is
/// captured.
/// </summary>
public sealed record InMemoryMembershipTests : IClassFixture<ExecutionFixture>
{
    private static readonly long[] StaticIds = [1, 4, 91, 500];

    private readonly ExecutionFixture _fixture;

    public InMemoryMembershipTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Big => _fixture.Big;

    private IColumn Code => _fixture.Code;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildId => _fixture.ChildId;

    private long[] PropertyIds { get; } = [2, 3, 92];

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void FiltersByMembershipInACapturedArray()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInACapturedList()
    {
        List<string> texts = ["plain", "", " padded ", "PLAIN", "plain "];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => texts.Contains(x.Cells[Text].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInACapturedHashSet()
    {
        HashSet<string> keys = ["a", "", "c", "A"];

        Enumeration.AssertSameElements(db =>
            db.Children.Where(x => keys.Contains(x.Cells[ChildKey].Value.TextValue))
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAnEmptyArray()
    {
        long[] none = [];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, bool>(
                x.Cells[ValueId].Value.TextValue,
                none.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
            ))
        );
    }

    [Fact]
    public void FiltersByAbsenceFromAnEmptyArray()
    {
        string[] none = [];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => !none.Contains(x.Cells[Text].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByMembershipInAnArrayHoldingNull()
    {
        long?[] numbers = [-1, null, 0];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    numbers.Contains(
                        string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                            ? null
                            : long.Parse(x.Cells[Big].Value.TextValue)
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByAbsenceFromAnArrayHoldingNull()
    {
        long?[] numbers = [-1, null, 0];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !numbers.Contains(
                        string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                            ? null
                            : long.Parse(x.Cells[Big].Value.TextValue)
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByAbsenceFromAnArrayWithoutNull()
    {
        long?[] numbers = [-1, 0];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !numbers.Contains(
                        string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                            ? null
                            : long.Parse(x.Cells[Big].Value.TextValue)
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByAbsenceFromAnInMemoryQueryWithoutNull()
    {
        long?[] numbers = [-1, 0];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !numbers
                        .AsQueryable()
                        .Contains(
                            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                                ? null
                                : long.Parse(x.Cells[Big].Value.TextValue)
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByAbsenceFromAnInMemoryQueryHoldingNull()
    {
        long?[] numbers = [-1, null, 0];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !numbers
                        .AsQueryable()
                        .Contains(
                            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                                ? null
                                : long.Parse(x.Cells[Big].Value.TextValue)
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersANullableCellByAbsenceFromAnEmptyArray()
    {
        Guid?[] none = [];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !none.Contains(
                        string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                            ? null
                            : Guid.Parse(x.Cells[Code].Value.TextValue)
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInALargeArray()
    {
        long[] ids = [.. Enumerable.Range(-5000, 10000).Select(x => (long)x * 3)];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInALargeInMemoryQuery()
    {
        long[] ids = [.. Enumerable.Range(-5000, 10000).Select(x => (long)x * 3)];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipOfUuidsInAnArray()
    {
        Guid[] codes =
        [
            Guid.Empty,
            Guid.Parse("a0b1c2d3-e4f5-a6b7-c8d9-e0f1a2b3c4d5"),
            Guid.Parse("12345678-1234-1234-1234-123456789abc"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    !string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                    && codes.Contains(Guid.Parse(x.Cells[Code].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAStaticField()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    StaticIds.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAnInstanceProperty()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    PropertyIds.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInTheResultOfAMethodCall()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    EvenIds(20).Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAReadOnlyCollection()
    {
        ReadOnlyCollection<long> ids = new ReadOnlyCollection<long>([1, 2, 500]);

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAnImmutableArray()
    {
        ImmutableArray<long> ids = [1, 2, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInARange()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    Enumerable
                        .Range(3, 6)
                        .Contains(int.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void UsesTheSameCapturedArrayTwice()
    {
        long[] ids = [1, 2, 4, 91];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Join(
                    ids,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, id) =>
                        new ValueTuple<long, int>(
                            id,
                            ids.AsQueryable().Count(y => y <= id)
                        )
                )
        );
    }

    [Fact]
    public void FiltersByAnyOfAnArrayMatchingTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Any(y => y == long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByAllOfAnArrayDifferingFromTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.All(y => y != long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    // VALUES has at least one row, so the translator writes no empty sequence.
    [Fact]
    public void RefusesAllOfAnEmptyArray()
    {
        long[] none = [];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x =>
                    none.All(y => y == long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByAnyOfAnInMemoryQueryAgainstANullableCell()
    {
        long?[] numbers = [null, -1];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    numbers
                        .AsQueryable()
                        .Any(y =>
                            y
                            == (
                                string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                                    ? null
                                    : long.Parse(x.Cells[Big].Value.TextValue)
                            )
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByTheCountOfAnArrayBelowTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Count(y => y < long.Parse(x.Cells[ValueId].Value.TextValue)) >= 2
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void ProjectsTheSumOfAnArrayBelowTheRowWhichMayBeEmpty()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long>(
                x.Cells[ValueId].Value.TextValue,
                ids.Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue)).Sum()
            ))
        );
    }

    [Fact]
    public void ProjectsTheNullableMaximumOfAnArrayBelowTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long?>(
                x.Cells[ValueId].Value.TextValue,
                ids.Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Max(y => (long?)y)
            ))
        );
    }

    [Fact]
    public void ThrowsForTheMinimumOfAnEmptyPartOfAnArray()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long>(
                x.Cells[ValueId].Value.TextValue,
                ids.Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue)).Min()
            ))
        );
    }

    [Fact]
    public void ProjectsTheAverageOfAnInMemoryQueryUpToTheRow()
    {
        long[] ids = [1, 1, 2, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, double>(
                    x.Cells[ValueId].Value.TextValue,
                    ids.AsQueryable()
                        .Where(y => y <= long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Average()
                ))
        );
    }

    [Fact]
    public void ProjectsTheAverageOfInMemoryDoublesUpToTheRow()
    {
        double[] numbers = [0.1, 0.2, 0.3, 1.0 / 3, 7.5];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, double, double>(
                    x.Cells[ValueId].Value.TextValue,
                    numbers.Sum(),
                    numbers
                        .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Sum()
                ))
        );
    }

    [Fact]
    public void FiltersByAllOfAnInMemoryQueryDifferingFromTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .All(y => y != long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByAllOfAnInMemoryQueryAgainstANullableCell()
    {
        long?[] numbers = [0, null];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, bool>(
                x.Cells[ValueId].Value.TextValue,
                numbers
                    .AsQueryable()
                    .All(y =>
                        y
                        != (
                            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                                ? null
                                : long.Parse(x.Cells[Big].Value.TextValue)
                        )
                    )
            ))
        );
    }

    [Fact]
    public void FiltersByTheCountOfAnInMemoryQueryBelowTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .Count(y => y < long.Parse(x.Cells[ValueId].Value.TextValue)) >= 2
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void ProjectsTheSumOfAnInMemoryQueryBelowTheRowWhichMayBeEmpty()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long, long>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Sum(),
                ids.AsQueryable()
                    .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .LongCount()
            ))
        );
    }

    [Fact]
    public void ProjectsTheNullableExtremesOfAnInMemoryQueryBelowTheRow()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long?, long?>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Max(y => (long?)y),
                ids.AsQueryable()
                    .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Min(y => (long?)y)
            ))
        );
    }

    [Fact]
    public void ThrowsForTheMinimumOfAnEmptyPartOfAnInMemoryQuery()
    {
        long[] ids = [1, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, long>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Min()
            ))
        );
    }

    [Fact]
    public void ProjectsTheSumOfInMemoryDoublesUpToTheRow()
    {
        double[] numbers = [0.1, 0.2, 0.3, 1.0 / 3, 7.5];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, double, double>(
                    x.Cells[ValueId].Value.TextValue,
                    numbers.Sum(),
                    numbers
                        .AsQueryable()
                        .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Sum()
                ))
        );
    }

    [Fact]
    public void ProjectsTheSumOfInMemoryDecimalsUpToTheRow()
    {
        decimal[] numbers = [0.1m, 0.25m, 1.125m, 79228162514264337593543950m];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                .Select(x => new ValueTuple<string, decimal>(
                    x.Cells[ValueId].Value.TextValue,
                    numbers
                        .AsQueryable()
                        .Where(y => y < long.Parse(x.Cells[ValueId].Value.TextValue))
                        .Sum()
                ))
        );
    }

    [Fact]
    public void ProjectsMembershipOfTheRowInAnArray()
    {
        string[] texts = ["plain", ""];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, bool, bool>(
                x.Cells[ValueId].Value.TextValue,
                texts.Contains(x.Cells[Text].Value.TextValue),
                !texts.Contains(
                    string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue
                )
            ))
        );
    }

    [Fact]
    public void CountsTheRowsInAnArray()
    {
        long[] ids = [1, 4, 91, 500, 1];

        Enumeration.AssertSameValue(db =>
            db.Values.Count(x =>
                ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
            )
        );
    }

    [Fact]
    public void FiltersByMembershipOfATupleInAnArrayOfTuples()
    {
        (long, string)[] pairs = [(1, "plain"), (2, ""), (3, "plain")];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    pairs.Contains(
                        new ValueTuple<long, string>(
                            long.Parse(x.Cells[ValueId].Value.TextValue),
                            x.Cells[Text].Value.TextValue
                        )
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipOfATupleInAnInMemoryQueryOfTuples()
    {
        (long, string)[] pairs = [(1, "plain"), (2, ""), (3, "plain")];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    pairs
                        .AsQueryable()
                        .Contains(
                            new ValueTuple<long, string>(
                                long.Parse(x.Cells[ValueId].Value.TextValue),
                                x.Cells[Text].Value.TextValue
                            )
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByAnyTupleOfAnInMemoryQueryMatchingTheRow()
    {
        (long Id, string Text)[] pairs = [(1, "plain"), (2, ""), (3, "plain")];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    pairs
                        .AsQueryable()
                        .Any(y =>
                            y.Id == long.Parse(x.Cells[ValueId].Value.TextValue)
                            && y.Text == x.Cells[Text].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAListMadeInsideTheQuery()
    {
        long[] ids = [1, 2, 600];

        Enumeration.AssertSameElements(db =>
            db.Children.Where(x =>
                    ids.ToList().Contains(long.Parse(x.Cells[ChildId].Value.TextValue))
                )
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAMaterializedListOfAStoredQuery()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Where(x =>
                    db.Values.Where(y => y.Cells[Text].Value.TextValue == "")
                        .Select(y => long.Parse(y.Cells[ValueId].Value.TextValue))
                        .ToList()
                        .Contains(long.Parse(x.Cells[ChildId].Value.TextValue))
                )
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersByMembershipInAStoredQueryCapturedAsASequence()
    {
        Enumeration.AssertSameElements(db =>
        {
            IEnumerable<long> ids = db
                .Values.Where(x => x.Cells[Text].Value.TextValue == "")
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue));
            return db
                .Children.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ChildId].Value.TextValue))
                )
                .Select(x => x.Cells[ChildId].Value.TextValue);
        });
    }

    [Fact]
    public void FiltersByMembershipInAStoredQueryCapturedAsAQuery()
    {
        Enumeration.AssertSameElements(db =>
        {
            IQueryable<long> ids = db
                .Values.Where(x => x.Cells[Text].Value.TextValue == "")
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue));
            return db
                .Children.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ChildId].Value.TextValue))
                )
                .Select(x => x.Cells[ChildId].Value.TextValue);
        });
    }

    private static long[] EvenIds(int count)
    {
        return [.. Enumerable.Range(0, count).Select(x => (long)x * 2)];
    }
}
