using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of orders the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Operators composed over ordered and paged rows must return, in one statement,
/// what the same query returns over the enumerated rows in memory: the same
/// elements in the same order, the same value or the same exception.
/// </summary>
public sealed record OrderingAndPagingTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public OrderingAndPagingTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Code => _fixture.Code;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void OrdersNullValuesFirst()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.OrderBy(x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Number].Value.TextValue)
                )
                .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void OrdersNullValuesLastWhenDescending()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.OrderByDescending(x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Number].Value.TextValue)
                )
                .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void OrdersRowsByParsedBoolAndUuidCells()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.Where(x => !x.Cells[ValueId].Value.TextValue.StartsWith('9'))
                .OrderBy(x => bool.Parse(x.Cells[Flag].Value.TextValue))
                .ThenByDescending(x => Guid.Parse(x.Cells[Code].Value.TextValue))
                .ThenBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void KeepsTheEarlierOrderAmongEqualKeysOfALaterOrder()
    {
        Enumeration.AssertSameOrder(db =>
            db.Children.OrderByDescending(x =>
                    long.Parse(x.Cells[ChildId].Value.TextValue)
                )
                .OrderBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void KeepsTheOrderOfFilteredAndJoinedRows()
    {
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Children, ChildId)
                .Where(x => x.Cells[ChildKey].Value.TextValue != "a")
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Parents, ParentId)
                .Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) => parent.Cells[ParentId].Value.TextValue
                )
        );
    }

    [Fact]
    public void ChainsSkipsAndTakes()
    {
        Enumeration.AssertSameOrder(db =>
            Ordered(db.Children, ChildId)
                .Skip(1)
                .Take(4)
                .Skip(1)
                .Take(2)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
        Enumeration.AssertSameOrder(db =>
            Ordered(db.Children, ChildId)
                .Take(2)
                .Take(5)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
        Enumeration.AssertSameOrder(db =>
            Ordered(db.Children, ChildId)
                .Skip(1)
                .Skip(2)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void ReversesPagedRows()
    {
        Enumeration.AssertSameOrder(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Reverse()
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
        Enumeration.AssertSameOrder(db =>
            db.Children.OrderBy(x => x.Cells[ChildKey].Value.TextValue)
                .ThenBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Reverse()
                .Take(3)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void AggregatesPagedRows()
    {
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Count(x => x.Cells[ChildKey].Value.TextValue == "a")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Skip(1)
                .Take(3)
                .Sum(x => long.Parse(x.Cells[ChildId].Value.TextValue))
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Select(x => x.Cells[ChildKey].Value.TextValue)
                .Distinct()
                .Count()
        );
    }

    [Fact]
    public void AnswersQuantifiersOverPagedRows()
    {
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(2)
                .Any(x => x.Cells[ChildKey].Value.TextValue == "c")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Skip(4)
                .All(x => x.Cells[ChildKey].Value.TextValue == "")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(2)
                .Select(x => x.Cells[ChildKey].Value.TextValue)
                .Contains("c")
        );
    }

    [Fact]
    public void FindsElementsAmongPagedRowsOnly()
    {
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(2)
                .Select(x => x.Cells[ChildKey].Value.TextValue)
                .First(x => x == "c")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .Single(x => x == "4")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .Last(x => x != "4")
        );
        Enumeration.AssertSameValue(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .ElementAtOrDefault(3)
        );
    }

    [Fact]
    public void JoinsAndGroupsPagedRows()
    {
        Enumeration.AssertSameElements(db =>
            Ordered(db.Parents, ParentId)
                .Take(2)
                .Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Cells[ChildId].Value.TextValue
                        )
                )
        );
        Enumeration.AssertSameElements(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
    }

    [Fact]
    public void FiltersRowsByPagedSubqueries()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    db.Children.OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                        .Take(2)
                        .Any(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    db.Children.OrderBy(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                        .Select(x => x.Cells[ChildKey].Value.TextValue)
                        .Take(2)
                        .Contains(parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void OrdersRowsByAggregatesOfSubqueries()
    {
        Enumeration.AssertSameOrder(db =>
            db.Parents.OrderByDescending(parent =>
                    db.Children.Count(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .ThenBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void KeepsTheFirstRowOfEachKeyOfOrderedRows()
    {
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Children, ChildId)
                .DistinctBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
        Enumeration.AssertSameValue(db =>
            OrderedDescending(db.Children, ChildId)
                .MinBy(x => x.Cells[ChildKey].Value.TextValue)
        );
        Enumeration.AssertSameValue(db =>
            OrderedDescending(db.Children, ChildId)
                .MaxBy(x => x.Cells[ChildNumber].Value.TextValue)
        );
    }

    [Fact]
    public void AggregatesAndOrdersSetOperations()
    {
        Enumeration.AssertSameValue(db =>
            Keys(db.Children).Concat(Keys(db.Parents)).Count()
        );
        Enumeration.AssertSameValue(db =>
            Keys(db.Children).Union(Keys(db.Parents)).Count()
        );
        Enumeration.AssertSameValue(db =>
            Keys(db.Children).Concat(Keys(db.Parents)).Max()
        );
        Enumeration.AssertSameValue(db =>
            Keys(db.Children).Except(Keys(db.Parents)).Any()
        );
        Enumeration.AssertSameOrder(db =>
            Keys(db.Children).Union(Keys(db.Parents)).OrderByDescending(x => x).Take(2)
        );
    }

    [Fact]
    public void CombinesPagedSidesOfSetOperations()
    {
        Enumeration.AssertSameElements(db =>
            OrderedDescending(db.Children, ChildId)
                .Take(2)
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .Concat(
                    Ordered(db.Parents, ParentId)
                        .Take(2)
                        .Select(x => x.Cells[ParentId].Value.TextValue)
                )
        );
    }

    [Fact(Skip = "#86: ThenBy after a second OrderBy sorts after the keys of the first.")]
    public void OrdersByTheKeysOfALaterOrderBeforeThoseOfAnEarlierOne()
    {
        Enumeration.AssertSameOrder(db =>
            db.Children.OrderBy(x => x.Cells[ChildKey].Value.TextValue)
                .Where(x => x.Cells[ChildId].Value.TextValue != "1")
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .OrderBy(x => x.Length)
                .ThenByDescending(x => x)
        );
    }

    [Fact(Skip = "#86: ThenBy after a second OrderBy sorts after the keys of the first.")]
    public void OrdersPagedRowsByTheKeysOfALaterOrderOnly()
    {
        Enumeration.AssertSameOrder(db =>
            Ordered(db.Children, ChildId)
                .Take(3)
                .OrderByDescending(x => x.Cells[ChildKey].Value.TextValue)
                .ThenByDescending(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact(Skip = "#86: DISTINCT does not keep the order of its source.")]
    public void KeepsTheOrderOfDistinctCells()
    {
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Children, ChildId)
                .Select(x => x.Cells[ChildKey].Value.TextValue)
                .Distinct()
        );
    }

    [Fact(Skip = "#86: GROUP BY does not keep the order of its source.")]
    public void KeepsTheOrderOfGroups()
    {
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Children, ChildId)
                .GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => x.Key)
        );
    }

    [Fact(Skip = "#86: UNION ALL does not keep the order of its sides.")]
    public void KeepsTheOrderOfConcatenatedSides()
    {
        Enumeration.AssertSameOrder(db =>
            OrderedDescending(db.Children, ChildId)
                .Select(x => x.Cells[ChildId].Value.TextValue)
                .Concat(
                    Ordered(db.Parents, ParentId)
                        .Select(x => x.Cells[ParentId].Value.TextValue)
                )
        );
    }

    /// <summary>
    /// The rows in the order of their ids, which no two rows of a table share.
    /// </summary>
    private static IOrderedQueryable<IRow> Ordered(IQueryable<IRow> rows, IColumn id)
    {
        return rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue));
    }

    private static IOrderedQueryable<IRow> OrderedDescending(
        IQueryable<IRow> rows,
        IColumn id
    )
    {
        return rows.OrderByDescending(x => long.Parse(x.Cells[id].Value.TextValue));
    }

    /// <summary>
    /// The keys of the rows, the second column of the parents and of the children.
    /// </summary>
    private IQueryable<string> Keys(IQueryable<IRow> rows)
    {
        return rows.Select(x => x.Cells[ParentKey].Value.TextValue);
    }
}
