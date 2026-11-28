using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Queries around <c>PostgreSqlRecursion.WithRecursive</c> over stored tables: the
/// operators after a walk (filters, orders, pages, aggregates, element operators
/// with and without elements, distinct, groups, joins, set operations), two walks
/// in one query, a walk seeded by or stepping through another walk, a walk inside
/// a predicate, a projection or a <c>SelectMany</c>, correlated or not, written
/// with the marker in the lambda or captured in a local, a method or an object,
/// and windows over a walk or a walk over a window. Each returns, in one statement,
/// what the same query returns over the walk computed level by level over the
/// enumerated rows (<see cref="RecursionReference"/>), or throws the same exception.
/// </summary>
public sealed record RecursionCompositionTests : IClassFixture<ExecutionFixture>
{
    private static readonly Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> Halving =
        level =>
            level.Where(x => x.Depth < 3).Select(x => new Hop(x.Id / 2, x.Depth + 1));

    private readonly ExecutionFixture _fixture;

    public RecursionCompositionTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void KeepsTheDuplicatesOfEveryLevel()
    {
        AssertSameValue(walk => walk.Count(), walk => walk.Count());
        AssertSameElements(
            walk => walk.Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            walk => walk.Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void RemovesTheDuplicatesOfAWalkWithDistinct()
    {
        AssertSameElements(
            walk =>
                walk.Select(x => new ValueTuple<long, long>(x.Id, x.Depth)).Distinct(),
            walk => walk.Select(x => new ValueTuple<long, long>(x.Id, x.Depth)).Distinct()
        );
        AssertSameElements(
            walk => walk.Select(x => x.Id).Distinct(),
            walk => walk.Select(x => x.Id).Distinct()
        );
    }

    [Fact]
    public void OrdersAndPagesAWalk()
    {
        AssertSameOrder(
            walk =>
                walk.OrderBy(x => x.Depth)
                    .ThenByDescending(x => x.Id)
                    .Skip(2)
                    .Take(5)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            walk =>
                walk.OrderBy(x => x.Depth)
                    .ThenByDescending(x => x.Id)
                    .Skip(2)
                    .Take(5)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void AggregatesAWalk()
    {
        AssertSameValue(
            walk => walk.LongCount(x => x.Id == 0),
            walk => walk.LongCount(x => x.Id == 0)
        );
        AssertSameValue(walk => walk.Sum(x => x.Id), walk => walk.Sum(x => x.Id));
        AssertSameValue(walk => walk.Min(x => x.Id), walk => walk.Min(x => x.Id));
        AssertSameValue(walk => walk.Max(x => x.Depth), walk => walk.Max(x => x.Depth));
        AssertSameValue(
            walk => walk.Average(x => (double)x.Id),
            walk => walk.Average(x => (double)x.Id)
        );
        AssertSameValue(
            walk => walk.Any(x => x.Depth == 3),
            walk => walk.Any(x => x.Depth == 3)
        );
        AssertSameValue(
            walk => walk.All(x => x.Id < 14),
            walk => walk.All(x => x.Id < 14)
        );
    }

    [Fact]
    public void ReadsAnElementOfAWalk()
    {
        AssertSameValue(
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).First(),
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).First()
        );
        AssertSameValue(
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).Last(),
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).Last()
        );
        AssertSameValue(
            walk => walk.Single(x => x.Id == 13),
            walk => walk.Single(x => x.Id == 13)
        );
        AssertSameValue(
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).ElementAt(4),
            walk => walk.OrderBy(x => x.Id).ThenBy(x => x.Depth).ElementAt(4)
        );
        AssertSameValue(
            walk => walk.MaxBy(x => (x.Id * 100) + x.Depth),
            walk => walk.MaxBy(x => (x.Id * 100) + x.Depth)
        );
    }

    [Fact]
    public void ThrowsAsLinqDoesForTheSingleElementOfManyRows()
    {
        AssertSameValue(
            walk => walk.Single(x => x.Id == 0),
            walk => walk.Single(x => x.Id == 0)
        );
        AssertSameValue(
            walk => walk.SingleOrDefault(x => x.Id == 0),
            walk => walk.SingleOrDefault(x => x.Id == 0)
        );
    }

    [Fact]
    public void ThrowsAsLinqDoesForTheElementsOfAnEmptyWalk()
    {
        AssertSameEmptyValue(walk => walk.First(), walk => walk.First());
        AssertSameEmptyValue(
            walk => walk.OrderBy(x => x.Id).Last(x => x.Id > 0),
            walk => walk.OrderBy(x => x.Id).Last(x => x.Id > 0)
        );
        AssertSameEmptyValue(walk => walk.Single(), walk => walk.Single());
        AssertSameEmptyValue(walk => walk.ElementAt(0), walk => walk.ElementAt(0));
        AssertSameEmptyValue(
            walk => walk.MinBy(x => x.Id),
            walk => walk.MinBy(x => x.Id)
        );
    }

    [Fact]
    public void ReturnsTheDefaultsForTheElementsOfAnEmptyWalk()
    {
        AssertSameEmptyValue(
            walk => walk.FirstOrDefault(),
            walk => walk.FirstOrDefault()
        );
        AssertSameEmptyValue(
            walk => walk.SingleOrDefault(),
            walk => walk.SingleOrDefault()
        );
        AssertSameEmptyValue(
            walk => walk.ElementAtOrDefault(3),
            walk => walk.ElementAtOrDefault(3)
        );
        AssertSameEmptyValue(
            walk => walk.Select(x => x.Id).FirstOrDefault(x => x > 0, -1),
            walk => walk.Select(x => x.Id).FirstOrDefault(x => x > 0, -1)
        );
        AssertSameEmptyValue(
            walk => walk.SingleOrDefault(x => x.Id > 0, new Hop(-1, -1)),
            walk => walk.SingleOrDefault(x => x.Id > 0, new Hop(-1, -1))
        );
    }

    [Fact]
    public void ReturnsTheGivenDefaultForTheFirstElementOfAnEmptyWalk()
    {
        AssertSameEmptyValue(
            walk => walk.Select(x => x.Id).FirstOrDefault(-1),
            walk => walk.Select(x => x.Id).FirstOrDefault(-1)
        );
        AssertSameEmptyValue(
            walk => walk.FirstOrDefault(new Hop(-1, -1)),
            walk => walk.FirstOrDefault(new Hop(-1, -1))
        );
    }

    [Fact]
    public void AggregatesAnEmptyWalkAsLinqDoes()
    {
        AssertSameEmptyValue(walk => walk.Count(), walk => walk.Count());
        AssertSameEmptyValue(walk => walk.Any(), walk => walk.Any());
        AssertSameEmptyValue(
            walk => walk.All(x => x.Id < 0),
            walk => walk.All(x => x.Id < 0)
        );
        AssertSameEmptyValue(walk => walk.Sum(x => x.Id), walk => walk.Sum(x => x.Id));
        AssertSameEmptyValue(walk => walk.Max(x => x.Id), walk => walk.Max(x => x.Id));
        AssertSameEmptyValue(
            walk => walk.Min(x => (long?)x.Id),
            walk => walk.Min(x => (long?)x.Id)
        );
        AssertSameEmptyValue(
            walk => walk.Average(x => (double)x.Id),
            walk => walk.Average(x => (double)x.Id)
        );
    }

    [Fact]
    public void GroupsAWalkByItsNodes()
    {
        AssertSameElements(
            walk =>
                walk.GroupBy(x => x.Id)
                    .Select(x => new ValueTuple<long, int, long, long>(
                        x.Key,
                        x.Count(),
                        x.Max(n => n.Depth),
                        x.Sum(n => n.Depth)
                    )),
            walk =>
                walk.GroupBy(x => x.Id)
                    .Select(x => new ValueTuple<long, int, long, long>(
                        x.Key,
                        x.Count(),
                        x.Max(n => n.Depth),
                        x.Sum(n => n.Depth)
                    ))
        );
    }

    [Fact]
    public void JoinsAWalkToAStoredTable()
    {
        Enumeration.AssertSameElements(
            db =>
                Walk(db)
                    .Join(
                        db.Values,
                        x => x.Id,
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        (hop, row) =>
                            new ValueTuple<long, long, string>(
                                hop.Id,
                                hop.Depth,
                                row.Cells[Text].Value.TextValue
                            )
                    ),
            db =>
                Reference(db)
                    .Join(
                        db.Values,
                        x => x.Id,
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        (hop, row) =>
                            new ValueTuple<long, long, string>(
                                hop.Id,
                                hop.Depth,
                                row.Cells[Text].Value.TextValue
                            )
                    )
        );
    }

    [Fact]
    public void JoinsAStoredTableToAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Join(
                    Walk(db),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Id,
                    (row, hop) => new ValueTuple<IRow, long>(row, hop.Depth)
                ),
            db =>
                db.Parents.Join(
                    Reference(db),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Id,
                    (row, hop) => new ValueTuple<IRow, long>(row, hop.Depth)
                )
        );
    }

    [Fact]
    public void LeftJoinsAWalkToAStoredTable()
    {
        Enumeration.AssertSameElements(
            db =>
                Walk(db)
                    .GroupJoin(
                        db.Parents,
                        x => x.Id,
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        (hop, rows) => new { Hop = hop, Rows = rows }
                    )
                    .SelectMany(
                        x => x.Rows.DefaultIfEmpty(),
                        (x, row) =>
                            new ValueTuple<long, long, string?>(
                                x.Hop.Id,
                                x.Hop.Depth,
                                row == null ? null : row.Cells[ParentKey].Value.TextValue
                            )
                    ),
            db =>
                Reference(db)
                    .GroupJoin(
                        db.Parents,
                        x => x.Id,
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        (hop, rows) => new { Hop = hop, Rows = rows }
                    )
                    .SelectMany(
                        x => x.Rows.DefaultIfEmpty(),
                        (x, row) =>
                            new ValueTuple<long, long, string?>(
                                x.Hop.Id,
                                x.Hop.Depth,
                                row?.Cells[ParentKey].Value.TextValue
                            )
                    )
        );
    }

    [Fact]
    public void JoinsTwoWalks()
    {
        Enumeration.AssertSameElements(
            db =>
                Walk(db)
                    .Where(x => x.Depth == 1)
                    .Join(
                        Doubling(db),
                        x => x.Id,
                        x => x.Id,
                        (one, other) =>
                            new ValueTuple<long, long, long>(
                                one.Id,
                                one.Depth,
                                other.Depth
                            )
                    ),
            db =>
                Reference(db)
                    .Where(x => x.Depth == 1)
                    .Join(
                        DoublingReference(db),
                        x => x.Id,
                        x => x.Id,
                        (one, other) =>
                            new ValueTuple<long, long, long>(
                                one.Id,
                                one.Depth,
                                other.Depth
                            )
                    )
        );
    }

    [Theory]
    [InlineData(nameof(Queryable.Concat))]
    [InlineData(nameof(Queryable.Union))]
    [InlineData(nameof(Queryable.Intersect))]
    [InlineData(nameof(Queryable.Except))]
    public void CombinesTwoWalks(string operation)
    {
        Enumeration.AssertSameElements(
            db =>
                Combined(
                    operation,
                    Walk(db).Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
                    Doubling(db).Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
                ),
            db =>
                Combined(
                    operation,
                    Reference(db)
                        .AsQueryable()
                        .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
                    DoublingReference(db)
                        .AsQueryable()
                        .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
                )
        );
    }

    [Fact]
    public void ConcatenatesAWalkOfRowsWithTheRowsOfItsTable()
    {
        Enumeration.AssertSameElements(
            db => RowWalk(db).Concat(db.Parents),
            db => RowReference(db).Concat(db.Parents)
        );
    }

    [Fact]
    public void ConcatenatesAWalkOfRowsWithTheRowsOfItsTableInAnotherSchema()
    {
        Enumeration.AssertSameElements(
            db => RowWalk(db).Concat(db.ArchivedParents),
            db => RowReference(db).Concat(db.ArchivedParents)
        );
    }

    [Fact]
    public void RefusesToConcatenateAWalkOfRowsWithTheRowsOfAnotherTable()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            RowWalk(db).Concat(db.Children)
        );

        Assert.Equal(
            "The query operator Concat combines rows of different tables, so it cannot "
                + "be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void CountsTheCellsOfTheRowsOfAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                RowWalk(db)
                    .Select(x => new ValueTuple<string, int, int>(
                        x.Cells[ParentId].Value.TextValue,
                        x.Cells.Count,
                        x.Cells.Keys.Count()
                    )),
            db =>
                RowReference(db)
                    .Select(x => new ValueTuple<string, int, int>(
                        x.Cells[ParentId].Value.TextValue,
                        x.Cells.Count,
                        x.Cells.Keys.Count()
                    ))
        );
    }

    [Fact]
    public void OrdersTheRowsOfAWalkByACell()
    {
        Enumeration.AssertSameOrder(
            db =>
                RowWalk(db)
                    .OrderBy(x => x.Cells[ParentKey].Value.TextValue)
                    .ThenBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select(x => x.Cells[ParentId].Value.TextValue),
            db =>
                RowReference(db)
                    .OrderBy(
                        x => x.Cells[ParentKey].Value.TextValue,
                        StringComparer.Ordinal
                    )
                    .ThenBy(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void SeedsAWalkWithAnotherWalk()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level.Where(x => x.Depth < 5).Select(x => new Hop(x.Id + 100, x.Depth + 1));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Walk(db).Where(x => x.Depth == 3), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(Reference(db).Where(x => x.Depth == 3), step.Compile())
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void StepsThroughAnotherWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).Where(x => x.Id < 4),
                        level =>
                            level
                                .Where(x => x.Depth < 3)
                                .Join(
                                    Doubling(db),
                                    x => x.Id * 2,
                                    x => x.Id,
                                    (hop, other) => new Hop(other.Id, hop.Depth + 1)
                                )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db).Where(x => x.Id < 4),
                        level =>
                            level
                                .Where(x => x.Depth < 3)
                                .Join(
                                    DoublingReference(db),
                                    x => x.Id * 2,
                                    x => x.Id,
                                    (hop, other) => new Hop(other.Id, hop.Depth + 1)
                                )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void FiltersRowsByAWalkWrittenInThePredicate()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> nodes = Nodes(db);
                return db
                    .Parents.Where(row =>
                        nodes
                            .WithRecursive(level =>
                                level
                                    .Where(x => x.Depth < 3)
                                    .Select(x => new Hop(x.Id / 2, x.Depth + 1))
                            )
                            .Any(x =>
                                x.Depth == 3
                                && x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                            )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db
                    .Parents.Where(row =>
                        walk.Any(x =>
                            x.Depth == 3
                            && x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void FiltersRowsByAWalkWrittenInThePredicateWithAConstantDepth()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> nodes = Nodes(db);
                return db
                    .Parents.Where(row =>
                        nodes
                            .WithRecursive(
                                level =>
                                    level
                                        .Where(x => x.Depth < 3)
                                        .Select(x => new Hop(x.Id / 2, x.Depth + 1)),
                                5
                            )
                            .Any(x =>
                                x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                            )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db
                    .Parents.Where(row =>
                        walk.Any(x =>
                            x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void FiltersRowsByAWalkWrittenInThePredicateWithACapturedDepth()
    {
        long depth = 5;

        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> nodes = Nodes(db);
                return db
                    .Parents.Where(row =>
                        nodes
                            .WithRecursive(
                                level =>
                                    level
                                        .Where(x => x.Depth < 3)
                                        .Select(x => new Hop(x.Id / 2, x.Depth + 1)),
                                depth
                            )
                            .Any(x =>
                                x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                            )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db
                    .Parents.Where(row =>
                        walk.Any(x =>
                            x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void FiltersRowsByMembershipInAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<long> reached = Walk(db)
                    .Where(x => x.Depth > 0)
                    .Select(x => x.Id);
                return db
                    .Values.Where(row =>
                        reached.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                long[] reached =
                [
                    .. Reference(db).Where(x => x.Depth > 0).Select(x => x.Id),
                ];
                return db
                    .Values.Where(row =>
                        reached.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void ProjectsTheSizeOfAWalkBesideEveryRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> walk = Walk(db);
                return db.Parents.Select(row => new ValueTuple<string, int, long>(
                    row.Cells[ParentId].Value.TextValue,
                    walk.Count(),
                    walk.Max(x => x.Depth)
                ));
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db.Parents.Select(row => new ValueTuple<string, int, long>(
                    row.Cells[ParentId].Value.TextValue,
                    walk.Count,
                    walk.Max(x => x.Depth)
                ));
            }
        );
    }

    [Fact]
    public void ProjectsAWalkSeededByEveryRow()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Select(row => new ValueTuple<string, int>(
                    row.Cells[ParentId].Value.TextValue,
                    db.Children.Where(x =>
                            x.Cells[ChildNumber].Value.TextValue
                            == row.Cells[ParentNumber].Value.TextValue
                        )
                        .Select(x => new Hop(
                            long.Parse(x.Cells[ChildId].Value.TextValue),
                            0
                        ))
                        .WithRecursive(level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new Hop(x.Id + 1, x.Depth + 1))
                        )
                        .Count()
                )),
            db =>
                db.Parents.Select(row => new ValueTuple<string, int>(
                    row.Cells[ParentId].Value.TextValue,
                    RecursionReference
                        .Levels(
                            db.Children.Where(x =>
                                    x.Cells[ChildNumber].Value.TextValue
                                    == row.Cells[ParentNumber].Value.TextValue
                                )
                                .Select(x => new Hop(
                                    long.Parse(x.Cells[ChildId].Value.TextValue),
                                    0
                                )),
                            level =>
                                level
                                    .Where(x => x.Depth < 2)
                                    .Select(x => new Hop(x.Id + 1, x.Depth + 1))
                        )
                        .Count
                ))
        );
    }

    [Fact]
    public void FiltersRowsByAWalkSeededByTheRow()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.Where(row =>
                        db.Children.Where(x =>
                                x.Cells[ChildNumber].Value.TextValue
                                == row.Cells[ParentNumber].Value.TextValue
                            )
                            .Select(x => new Hop(
                                long.Parse(x.Cells[ChildId].Value.TextValue),
                                0
                            ))
                            .WithRecursive(level =>
                                level
                                    .Where(x => x.Depth < 2)
                                    .Select(x => new Hop(x.Id * 2, x.Depth + 1))
                            )
                            .Any(x => x.Id == 4)
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue),
            db =>
                db.Parents.Where(row =>
                        RecursionReference
                            .Levels(
                                db.Children.Where(x =>
                                        x.Cells[ChildNumber].Value.TextValue
                                        == row.Cells[ParentNumber].Value.TextValue
                                    )
                                    .Select(x => new Hop(
                                        long.Parse(x.Cells[ChildId].Value.TextValue),
                                        0
                                    )),
                                level =>
                                    level
                                        .Where(x => x.Depth < 2)
                                        .Select(x => new Hop(x.Id * 2, x.Depth + 1))
                            )
                            .Any(x => x.Id == 4)
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void FlattensAWalkSeededByEveryRow()
    {
        Enumeration.AssertSameElements(
            db =>
                db.Parents.SelectMany(
                    row =>
                        db.Children.Where(x =>
                                x.Cells[ChildNumber].Value.TextValue
                                == row.Cells[ParentNumber].Value.TextValue
                            )
                            .Select(x => new Hop(
                                long.Parse(x.Cells[ChildId].Value.TextValue),
                                0
                            ))
                            .WithRecursive(level =>
                                level
                                    .Where(x => x.Depth < 2)
                                    .Select(x => new Hop(x.Id + 10, x.Depth + 1))
                            ),
                    (row, hop) =>
                        new ValueTuple<string, long, long>(
                            row.Cells[ParentId].Value.TextValue,
                            hop.Id,
                            hop.Depth
                        )
                ),
            db =>
                db.Parents.SelectMany(
                    row =>
                        RecursionReference.Levels(
                            db.Children.Where(x =>
                                    x.Cells[ChildNumber].Value.TextValue
                                    == row.Cells[ParentNumber].Value.TextValue
                                )
                                .Select(x => new Hop(
                                    long.Parse(x.Cells[ChildId].Value.TextValue),
                                    0
                                )),
                            level =>
                                level
                                    .Where(x => x.Depth < 2)
                                    .Select(x => new Hop(x.Id + 10, x.Depth + 1))
                        ),
                    (row, hop) =>
                        new ValueTuple<string, long, long>(
                            row.Cells[ParentId].Value.TextValue,
                            hop.Id,
                            hop.Depth
                        )
                )
        );
    }

    [Fact]
    public void ProjectsAWalkWhoseStepReadsTheRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> nodes = Nodes(db).Where(x => x.Id < 3);
                return db.Parents.Select(row => new ValueTuple<string, int>(
                    row.Cells[ParentId].Value.TextValue,
                    nodes
                        .WithRecursive(level =>
                            level
                                .Where(x =>
                                    x.Id
                                    < long.Parse(row.Cells[ParentId].Value.TextValue) * 4
                                )
                                .Select(x => new Hop(x.Id * 2, x.Depth + 1))
                        )
                        .Count()
                ));
            },
            db =>
            {
                IQueryable<Hop> nodes = Nodes(db).Where(x => x.Id < 3);
                return db.Parents.Select(row => new ValueTuple<string, int>(
                    row.Cells[ParentId].Value.TextValue,
                    RecursionReference
                        .Levels(
                            nodes,
                            level =>
                                level
                                    .Where(x =>
                                        x.Id
                                        < long.Parse(row.Cells[ParentId].Value.TextValue)
                                            * 4
                                    )
                                    .Select(x => new Hop(x.Id * 2, x.Depth + 1))
                        )
                        .Count
                ));
            }
        );
    }

    [Fact]
    public void FiltersRowsByAWalkWhoseSeedAndStepReadOneCapturedTable()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> parents = db.Parents;
                return db
                    .Values.Where(row =>
                        parents
                            .Where(x => x.Cells[ParentId].Value.TextValue == "2")
                            .WithRecursive(level =>
                                parents.Join(
                                    level,
                                    x => x.Cells[ParentNumber].Value.TextValue,
                                    x =>
                                        (
                                            long.Parse(
                                                x.Cells[ParentNumber].Value.TextValue
                                            ) - 1
                                        ).ToString(),
                                    (parent, before) => parent
                                )
                            )
                            .Any(x =>
                                x.Cells[ParentId].Value.TextValue
                                == row.Cells[ValueId].Value.TextValue
                            )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                List<IRow> walk = RowReference(db);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Cells[ParentId].Value.TextValue
                            == row.Cells[ValueId].Value.TextValue
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void FlattensAWalkFilteredByEveryRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<Hop> walk = Walk(db);
                return db.Parents.SelectMany(
                    row =>
                        walk.Where(x =>
                            x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                        ),
                    (row, hop) =>
                        new ValueTuple<string, long, long>(
                            row.Cells[ParentKey].Value.TextValue,
                            hop.Id,
                            hop.Depth
                        )
                );
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db.Parents.SelectMany(
                    row =>
                        walk.Where(x =>
                            x.Id == long.Parse(row.Cells[ParentId].Value.TextValue)
                        ),
                    (row, hop) =>
                        new ValueTuple<string, long, long>(
                            row.Cells[ParentKey].Value.TextValue,
                            hop.Id,
                            hop.Depth
                        )
                );
            }
        );
    }

    [Fact]
    public void FlattensTheRowsOfAWalkPerRow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> walk = RowWalk(db);
                return db
                    .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 3)
                    .SelectMany(
                        row => walk,
                        (row, parent) =>
                            new ValueTuple<string, IRow>(
                                row.Cells[ValueId].Value.TextValue,
                                parent
                            )
                    );
            },
            db =>
            {
                List<IRow> walk = RowReference(db);
                return db
                    .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 3)
                    .SelectMany(
                        row => walk,
                        (row, parent) =>
                            new ValueTuple<string, IRow>(
                                row.Cells[ValueId].Value.TextValue,
                                parent
                            )
                    );
            }
        );
    }

    // Also without a walk: any composed query of rows captured as IEnumerable<IRow>
    // and read by an Enumerable operator (a bare table or AsQueryable() works).
    [Fact]
    public void InlinesAWalkOfRowsCapturedAsASequence()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IEnumerable<IRow> walk = RowWalk(db);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Cells[ParentId].Value.TextValue
                            == row.Cells[ValueId].Value.TextValue
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                List<IRow> walk = RowReference(db);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Cells[ParentId].Value.TextValue
                            == row.Cells[ValueId].Value.TextValue
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void EnumeratesAWalkOfUntypedElements()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        IQueryable<Hop> walk = Walk(ExecutionTables.Stored(_fixture, connection));
        IQueryable untyped = walk.Provider.CreateQuery(walk.Expression);

        List<Hop> elements = [.. untyped.Cast<Hop>()];
        List<object> objects = [.. untyped.Cast<object>()];
        List<Hop> filtered = [.. untyped.OfType<Hop>()];

        List<Hop> expected = Reference(ExecutionTables.Enumerated(_fixture));
        Assert.Equal(
            expected.Order(Ordering.Instance),
            elements.Order(Ordering.Instance)
        );
        Assert.Equal(
            expected.Order(Ordering.Instance),
            objects.Cast<Hop>().Order(Ordering.Instance)
        );
        Assert.Equal(
            expected.Order(Ordering.Instance),
            filtered.Order(Ordering.Instance)
        );
        Assert.Equal(3, connection.Statements.Count);
    }

    [Fact]
    public void InlinesAWalkReturnedByAMethodOfAnObject()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                Walks walks = new Walks(this, db);
                return db
                    .Values.Where(row =>
                        walks
                            .Halving()
                            .Any(x =>
                                x.Depth == 2
                                && x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Depth == 2
                            && x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void InlinesAWalkHeldByAPropertyOfAnObject()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                Walks walks = new Walks(this, db);
                return db
                    .Values.Where(row =>
                        walks.Walk.Any(x =>
                            x.Depth == 1
                            && x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                List<Hop> walk = Reference(db);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Depth == 1
                            && x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                        )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Fact]
    public void StepsThroughATableCapturedByAMethod()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).Where(x => x.Id < 3),
                        level =>
                            ParentsOf(db)
                                .Join(
                                    level.Where(x => x.Depth < 3),
                                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                                    x => x.Id,
                                    (row, hop) => new Hop(hop.Id * 2, hop.Depth + 1)
                                )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db).Where(x => x.Id < 3),
                        level =>
                            ParentsOf(db)
                                .Join(
                                    level.Where(x => x.Depth < 3),
                                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                                    x => x.Id,
                                    (row, hop) => new Hop(hop.Id * 2, hop.Depth + 1)
                                )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void RanksTheRowsOfAWalkInAWindow()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.Over(
                    Walk(db),
                    x => x.Depth,
                    rows => rows.OrderBy(x => x.Id),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long, long, int>(
                            x.Id,
                            x.Depth,
                            w.Rank(),
                            w.Rows.Count()
                        )
                ),
            db =>
            {
                List<Hop> walk = Reference(db);
                return walk.Select(x => new ValueTuple<long, long, long, int>(
                    x.Id,
                    x.Depth,
                    walk.Count(y => y.Depth == x.Depth && y.Id < x.Id) + 1,
                    walk.Count(y => y.Depth == x.Depth && y.Id <= x.Id)
                ));
            }
        );
    }

    [Fact]
    public void WalksAWindowedSeed()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level.Where(x => x.Depth < 15).Select(x => new Hop(x.Id, x.Depth + 5));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        MarkerCalls.Over(
                            Nodes(db),
                            null,
                            rows => rows.OrderBy(x => x.Id),
                            null,
                            (x, w) => new Hop(x.Id, w.RowNumber())
                        ),
                        step
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db)
                            .AsEnumerable()
                            .OrderBy(x => x.Id)
                            .Select((x, index) => new Hop(x.Id, index + 1)),
                        step.Compile()
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    // PostgreSQL computes the window of a step over the rows of the level before.
    [Fact]
    public void NumbersTheRowsOfEachLevelInAWindowOfTheStep()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db),
                        level =>
                            PostgreSqlWindows.Over(
                                level.Where(x => x.Depth < 2),
                                null,
                                rows => rows.OrderBy(x => x.Id),
                                null,
                                (x, w) => new Hop(x.Id, x.Depth + w.RowNumber())
                            )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .OrderBy(x => x.Id)
                                .Select((x, index) => new Hop(x.Id, x.Depth + index + 1))
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void RefusesAStepAggregatingTheLevelBesideItsRows()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Nodes(db),
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new Hop(x.Id, x.Depth + level.Count()))
            )
        );
    }

    // PostgreSQL pages the rows of each level, the working table, on their own.
    [Fact]
    public void PagesTheRowsOfEachLevelInAPagedStep()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 3)
                .OrderByDescending(x => x.Id)
                .Select(x => new Hop(x.Id - 1, x.Depth + 1))
                .Skip(1)
                .Take(3);

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Nodes(db), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(Nodes(db), step.Compile())
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void RemovesTheDuplicatesOfEachLevelWithADistinctStep()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 3)
                .Select(x => new Hop(x.Id / 2, x.Depth + 1))
                .Distinct();

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Nodes(db), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(Nodes(db), step.Compile())
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void JoinsAStoredTableOnTheOptionalSideOfAStep()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 2)
                .GroupJoin(
                    _children,
                    x => x.Id,
                    x =>
                        string.IsNullOrEmpty(x.Cells[ChildNumber].Value.TextValue)
                            ? -1
                            : long.Parse(x.Cells[ChildNumber].Value.TextValue),
                    (hop, rows) => new { Hop = hop, Rows = rows }
                )
                .SelectMany(
                    x => x.Rows.DefaultIfEmpty(),
                    (x, row) =>
                        new Hop(
                            row == null
                                ? -x.Hop.Id
                                : long.Parse(row.Cells[ChildId].Value.TextValue),
                            x.Hop.Depth + 1
                        )
                );

        Enumeration.AssertSameElements(
            db =>
            {
                _children = db.Children;
                return MarkerCalls
                    .WithRecursive(Nodes(db).Where(x => x.Id < 5), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth));
            },
            db =>
            {
                _children = db.Children;
                return RecursionReference
                    .Levels(Nodes(db).Where(x => x.Id < 5), step.Compile())
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth));
            }
        );
    }

    [Fact]
    public void CrossJoinsALevelWithAStoredTable()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            _children.SelectMany(
                x => level.Where(n => n.Depth < 2),
                (row, hop) =>
                    new Hop(
                        (hop.Id * 100) + long.Parse(row.Cells[ChildId].Value.TextValue),
                        hop.Depth + 1
                    )
            );

        Enumeration.AssertSameElements(
            db =>
            {
                _children = db.Children;
                return MarkerCalls
                    .WithRecursive(Nodes(db).Where(x => x.Id < 3), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth));
            },
            db =>
            {
                _children = db.Children;
                return RecursionReference
                    .Levels(Nodes(db).Where(x => x.Id < 3), step.Compile())
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth));
            }
        );
    }

    /// <summary>
    /// The children table the steps above read through a field, replaced per run.
    /// </summary>
    private IQueryable<IRow> _children = Enumerable.Empty<IRow>().AsQueryable();

    /// <summary>
    /// The ids of the <c>Values</c> table without NULL cells, as level 0.
    /// </summary>
    private IQueryable<Hop> Nodes(ExecutionTables db)
    {
        return db
            .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
            .Select(x => new Hop(long.Parse(x.Cells[ValueId].Value.TextValue), 0));
    }

    private static IQueryable<IRow> ParentsOf(ExecutionTables db)
    {
        return db.Parents;
    }

    /// <summary>
    /// The nodes halved level by level: a walk with duplicates in every level.
    /// </summary>
    private IQueryable<Hop> Walk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(Nodes(db), Halving);
    }

    private List<Hop> Reference(ExecutionTables db)
    {
        return RecursionReference.Levels(Nodes(db), Halving.Compile());
    }

    private IQueryable<Hop> EmptyWalk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(Nodes(db).Where(x => x.Id > 1000), Halving);
    }

    private List<Hop> EmptyReference(ExecutionTables db)
    {
        return RecursionReference.Levels(
            Nodes(db).Where(x => x.Id > 1000),
            Halving.Compile()
        );
    }

    private static readonly Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> Twice =
        level => level.Where(x => x.Id < 20).Select(x => new Hop(x.Id * 2, x.Depth + 1));

    /// <summary>
    /// The nodes doubled level by level below 20.
    /// </summary>
    private IQueryable<Hop> Doubling(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(Nodes(db).Where(x => x.Id < 4), Twice);
    }

    private List<Hop> DoublingReference(ExecutionTables db)
    {
        return RecursionReference.Levels(Nodes(db).Where(x => x.Id < 4), Twice.Compile());
    }

    /// <summary>
    /// The parents reached from parent 2 by numbers one smaller, then the parents of
    /// the archive whose id is a parent's number.
    /// </summary>
    private IQueryable<IRow> RowWalk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x =>
                        (
                            long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1
                        ).ToString(),
                    (parent, before) => parent
                )
        );
    }

    private List<IRow> RowReference(ExecutionTables db)
    {
        return RecursionReference.Levels(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x =>
                        (
                            long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1
                        ).ToString(),
                    (parent, before) => parent
                )
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
            nameof(Queryable.Concat) => first.Concat(second),
            nameof(Queryable.Union) => first.Union(second),
            nameof(Queryable.Intersect) => first.Intersect(second),
            _ => first.Except(second),
        };
    }

    private void AssertSameElements<T>(
        Func<IQueryable<Hop>, IQueryable<T>> stored,
        Func<IQueryable<Hop>, IEnumerable<T>> enumerated
    )
    {
        Enumeration.AssertSameElements(
            db => stored(Walk(db)),
            db => enumerated(Reference(db).AsQueryable())
        );
    }

    private void AssertSameOrder<T>(
        Func<IQueryable<Hop>, IQueryable<T>> stored,
        Func<IQueryable<Hop>, IEnumerable<T>> enumerated
    )
    {
        Enumeration.AssertSameOrder(
            db => stored(Walk(db)),
            db => enumerated(Reference(db).AsQueryable())
        );
    }

    private void AssertSameValue<T>(
        Func<IQueryable<Hop>, T> stored,
        Func<IQueryable<Hop>, T> enumerated
    )
    {
        Enumeration.AssertSameValue(
            db => stored(Walk(db)),
            db => enumerated(Reference(db).AsQueryable())
        );
    }

    /// <summary>
    /// The value over a walk of no rows, translated in one statement, is the value
    /// LINQ to Objects returns over no rows, or the exception it throws.
    /// </summary>
    private void AssertSameEmptyValue<T>(
        Func<IQueryable<Hop>, T> stored,
        Func<IQueryable<Hop>, T> enumerated
    )
    {
        Enumeration.AssertSameValue(
            db => stored(EmptyWalk(db)),
            db => enumerated(EmptyReference(db).AsQueryable())
        );
    }

    /// <summary>
    /// Nodes by id, then by level.
    /// </summary>
    private sealed class Ordering : IComparer<Hop>
    {
        public static readonly Ordering Instance = new Ordering();

        public int Compare(Hop? x, Hop? y)
        {
            int id = x!.Id.CompareTo(y!.Id);
            return id != 0 ? id : x.Depth.CompareTo(y.Depth);
        }
    }

    /// <summary>
    /// Walks an object reads from its members, which captured queries are inlined
    /// from.
    /// </summary>
    private sealed class Walks(RecursionCompositionTests tests, ExecutionTables db)
    {
        private readonly RecursionCompositionTests _tests = tests;

        private readonly ExecutionTables _db = db;

        public IQueryable<Hop> Walk => _tests.Walk(_db);

        public IQueryable<Hop> Halving()
        {
            return _tests.Walk(_db);
        }
    }
}
