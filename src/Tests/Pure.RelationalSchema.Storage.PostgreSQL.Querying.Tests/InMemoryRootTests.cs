using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Queries whose root is an in-memory sequence of several elements, written as
/// <c>VALUES</c>, that read stored tables in their operators, run through the
/// provider of a stored table: filtered, ordered, paged, grouped, distinct and
/// aggregated, and executed for one element or one value; each returns in one
/// statement what LINQ to Objects returns over the enumerated rows.
/// </summary>
public sealed record InMemoryRootTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] Labels =
    [
        (1, "one"),
        (2, null),
        (4, ""),
        (91, "z"),
        (91, null),
        (500, "unmatched"),
    ];

    private readonly ExecutionFixture _fixture;

    public InMemoryRootTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void FiltersAnInMemoryRootByStoredRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<string?>(
                Labels
                    .AsQueryable()
                    .Where(x =>
                        db.Children.Any(y =>
                            long.Parse(y.Cells[ChildId].Value.TextValue) == x.Id
                        )
                    )
                    .Select(x => x.Label)
                    .Expression
            )
        );
    }

    [Fact]
    public void FiltersAnInMemoryRootByCountsOfStoredRowsThroughAnAnonymousObject()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<long>(
                Labels
                    .AsQueryable()
                    .Select(x => new
                    {
                        x.Id,
                        Count = db.Values.Count(y =>
                            long.Parse(y.Cells[ValueId].Value.TextValue) <= x.Id
                        ),
                    })
                    .Where(x => x.Count > 4)
                    .Select(x => x.Id)
                    .Expression
            )
        );
    }

    [Fact]
    public void PagesAnOrderedInMemoryRootJoinedToRows()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, string>>(
                Labels
                    .AsQueryable()
                    .OrderByDescending(x => x.Id)
                    .ThenBy(x => x.Label == null)
                    .Skip(1)
                    .Take(3)
                    .Join(
                        db.Values,
                        x => x.Id,
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        (label, row) =>
                            new { label.Id, Text = row.Cells[Text].Value.TextValue }
                    )
                    .OrderBy(x => x.Id)
                    .Select(x => new ValueTuple<long, string>(x.Id, x.Text))
                    .Expression
            )
        );
    }

    [Fact]
    public void GroupsAnInMemoryRootWithCountsOfStoredRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, int, int>>(
                Labels
                    .AsQueryable()
                    .GroupBy(x => x.Id)
                    .Select(x => new ValueTuple<long, int, int>(
                        x.Key,
                        x.Count(),
                        db.Children.Count(y =>
                            long.Parse(y.Cells[ChildId].Value.TextValue) == x.Key
                        )
                    ))
                    .Expression
            )
        );
    }

    [Fact]
    public void ReadsTheDistinctElementsOfAnInMemoryRootBesideStoredRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<long>(
                Labels
                    .AsQueryable()
                    .Select(x => x.Id)
                    .Distinct()
                    .Where(x =>
                        !db.Values.Any(y =>
                            long.Parse(y.Cells[ValueId].Value.TextValue) == x
                        )
                    )
                    .Expression
            )
        );
    }

    [Fact]
    public void CountsAnInMemoryRootFilteredByStoredRows()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Provider.Execute<int>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Count),
                    [typeof(ValueTuple<long, string?>)],
                    Labels
                        .AsQueryable()
                        .Where(x =>
                            db.Children.Any(y =>
                                long.Parse(y.Cells[ChildId].Value.TextValue) == x.Id
                            )
                        )
                        .Expression
                )
            )
        );
    }

    [Fact]
    public void SumsAnInMemoryRootWeightedByStoredRows()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Provider.Execute<long>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Sum),
                    [],
                    Labels
                        .AsQueryable()
                        .Select(x =>
                            x.Id
                            * db.Children.LongCount(y =>
                                y.Cells[ChildKey].Value.TextValue == "a"
                            )
                        )
                        .Expression
                )
            )
        );
    }

    [Fact]
    public void ReadsTheFirstElementOfAnInMemoryRootMatchingStoredRows()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Provider.Execute<ValueTuple<long, string?>>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.First),
                    [typeof(ValueTuple<long, string?>)],
                    Labels
                        .AsQueryable()
                        .Where(x =>
                            db.Children.Any(y =>
                                long.Parse(y.Cells[ChildId].Value.TextValue) == x.Id
                            )
                        )
                        .OrderByDescending(x => x.Id)
                        .ThenBy(x => x.Label == null)
                        .Select(x => new ValueTuple<long, string?>(x.Id, x.Label))
                        .Expression
                )
            )
        );
    }

    [Fact]
    public void OrdersTiesOfAnInMemoryRootInTheOrderOfTheSequence()
    {
        (long Key, long Position)[] items =
        [
            .. Enumerable.Range(0, 200).Select(x => ((long)(x * 7 % 3), (long)x)),
        ];

        Enumeration.AssertSameOrder(db =>
            db.Values.Provider.CreateQuery<long>(
                items
                    .AsQueryable()
                    .Where(x => db.Values.Any())
                    .OrderBy(x => x.Key)
                    .Select(x => x.Position)
                    .Expression
            )
        );
    }

    [Fact]
    public void OrdersTiesOfAnInMemorySequenceJoinedToARowInTheOrderOfTheSequence()
    {
        (long Key, long Position)[] items =
        [
            .. Enumerable.Range(0, 200).Select(x => ((long)(x * 7 % 3), (long)x)),
        ];

        Enumeration.AssertSameOrder(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => items, (row, item) => item)
                .OrderBy(x => x.Key)
                .Select(x => x.Position)
        );
    }

    [Fact]
    public void ThrowsForTheFirstElementOfAnInMemoryRootMatchingNoRow()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Provider.Execute<long>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.First),
                    [typeof(long)],
                    Labels
                        .AsQueryable()
                        .Where(x =>
                            db.Children.Any(y =>
                                long.Parse(y.Cells[ChildId].Value.TextValue)
                                == x.Id + 1000
                            )
                        )
                        .Select(x => x.Id)
                        .Expression
                )
            )
        );
    }

    [Fact]
    public void ConcatenatesAnInMemoryRootWithStoredValues()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<long>(
                Labels
                    .AsQueryable()
                    .Select(x => x.Id)
                    .Concat(
                        db.Children.Select(x =>
                            long.Parse(x.Cells[ChildId].Value.TextValue)
                        )
                    )
                    .Expression
            )
        );
    }
}
