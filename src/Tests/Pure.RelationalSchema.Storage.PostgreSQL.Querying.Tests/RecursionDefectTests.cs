#pragma warning disable xUnit1004 // Tests blocked on a fix are skipped with the reason.

using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A node reached at a level, with the tags it carries, which may be null.
/// </summary>
internal sealed record TaggedHop(long Id, long Depth, string[]? Tags);

/// <summary>
/// Walks of <c>PostgreSqlRecursion.WithRecursive</c> over stored tables in the
/// shapes the translator's issue #128 lists as defects: a seed whose array column
/// is a null or an empty array, a walk deeper than its maximum depth under a query
/// that reads none of its rows, and a recursion correlated with the level of an
/// outer step under <c>SelectMany</c>. Each returns, in one statement, what the walk
/// computed level by level over the enumerated rows returns
/// (<see cref="RecursionReference"/>), or throws the same exception.
/// </summary>
public sealed record RecursionDefectTests : IClassFixture<ExecutionFixture>
{
    private static readonly string[] Labels = ["a", "b"];

    private readonly ExecutionFixture _fixture;

    public RecursionDefectTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn ParentId => _fixture.ParentId;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    // --------------------------------------------------------- array columns of seeds

    [Fact(
        Skip = "#111 (translator #128): a null array of the seed is written NULL AS "
            + "\"Tags\", not cast to text[], so PostgreSQL rejects the step's "
            + "ARRAY['a', 'b'] (42804: recursive query column 3 has type text in "
            + "non-recursive term but type text[] overall)"
    )]
    public void CarriesTheArraysOfTheStepFromANullSeedColumn()
    {
        string[] labels = Labels;
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Seed(db).Select(x => new TaggedHop(x.Id, x.Depth, null)),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new TaggedHop(x.Id + 1, x.Depth + 1, labels))
                    )
                    .Select(x => new ValueTuple<long, long, int>(
                        x.Id,
                        x.Depth,
                        x.Tags == null ? -1 : x.Tags.Length
                    )),
            db =>
                RecursionReference
                    .Levels(
                        Seed(db).Select(x => new TaggedHop(x.Id, x.Depth, null)),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new TaggedHop(x.Id + 1, x.Depth + 1, labels))
                    )
                    .Select(x => new ValueTuple<long, long, int>(
                        x.Id,
                        x.Depth,
                        x.Tags == null ? -1 : x.Tags.Length
                    ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): an empty array of the seed is not cast to "
            + "text[], so PostgreSQL rejects the step's ARRAY['a', 'b'] (42804: "
            + "recursive query column 3 has type text in non-recursive term but type "
            + "text[] overall)"
    )]
    public void CarriesTheArraysOfTheStepFromAnEmptyArraySeedColumn()
    {
        string[] labels = Labels;
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Seed(db).Select(x => new TaggedHop(x.Id, x.Depth, new string[0])),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new TaggedHop(x.Id + 1, x.Depth + 1, labels))
                    )
                    .Select(x => new ValueTuple<long, long, int>(
                        x.Id,
                        x.Depth,
                        x.Tags == null ? -1 : x.Tags.Length
                    )),
            db =>
                RecursionReference
                    .Levels(
                        Seed(db).Select(x => new TaggedHop(x.Id, x.Depth, new string[0])),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new TaggedHop(x.Id + 1, x.Depth + 1, labels))
                    )
                    .Select(x => new ValueTuple<long, long, int>(
                        x.Id,
                        x.Depth,
                        x.Tags == null ? -1 : x.Tags.Length
                    ))
        );
    }

    // ------------------------------------- the maximum depth under queries reading none

    [Fact(
        Skip = "#111 (translator #128): the maximum depth does not fail when the query "
            + "around the walk filters out every row: WHERE FALSE around the derived "
            + "table lets PostgreSQL skip the recursion, so no rows are returned "
            + "where the README promises PostgreSQL's error"
    )]
    public void FailsAWalkDeeperThanTheMaximumDepthUnderAFalseFilter()
    {
        Enumeration.AssertSameElements(
            db => Endless(db, 2).Where(x => false),
            db => InMemoryEndless(db, 2).Where(x => false)
        );
    }

    [Fact(
        Skip = "#111 (translator #128): the maximum depth does not fail when the walk "
            + "is joined with no rows: PostgreSQL skips the recursion, so no rows are "
            + "returned where the README promises PostgreSQL's error"
    )]
    public void FailsAWalkDeeperThanTheMaximumDepthJoinedWithNoRows()
    {
        Enumeration.AssertSameElements(
            db =>
                Endless(db, 2)
                    .Join(
                        db.Parents.Where(x =>
                            x.Cells[ParentId].Value.TextValue == "none"
                        ),
                        x => x.Id.ToString(),
                        x => x.Cells[ParentId].Value.TextValue,
                        (hop, parent) =>
                            new ValueTuple<long, string>(
                                hop.Id,
                                parent.Cells[ParentId].Value.TextValue
                            )
                    ),
            db =>
                InMemoryEndless(db, 2)
                    .Join(
                        db.Parents.Where(x =>
                            x.Cells[ParentId].Value.TextValue == "none"
                        ),
                        x => x.Id.ToString(),
                        x => x.Cells[ParentId].Value.TextValue,
                        (hop, parent) =>
                            new ValueTuple<long, string>(
                                hop.Id,
                                parent.Cells[ParentId].Value.TextValue
                            )
                    )
        );
    }

    // ---------------------------------------- a recursion correlated with a level

    [Fact(
        Skip = "#111 (translator #128): a recursion whose seed reads the level of an "
            + "outer step is joined in SelectMany as INNER JOIN (WITH RECURSIVE ...) "
            + "AS t6 ON TRUE, without LATERAL (42P01: invalid reference to "
            + "FROM-clause entry for table t2); see also #143"
    )]
    public void FlattensARecursionCorrelatedWithTheLevelOfTheStep()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                    Seed(db).Select(x => x.Id),
                    level =>
                        level.SelectMany(
                            id =>
                                db.Values.Where(x =>
                                        long.Parse(x.Cells[ValueId].Value.TextValue)
                                        == id + 1
                                    )
                                    .Select(x =>
                                        long.Parse(x.Cells[ValueId].Value.TextValue)
                                    )
                                    .WithRecursive(inner => inner.Where(x => x < 0)),
                            (id, below) => below
                        )
                ),
            db =>
                RecursionReference.Levels(
                    Seed(db).Select(x => x.Id),
                    level =>
                        level
                            .AsEnumerable()
                            .SelectMany(
                                id =>
                                    RecursionReference.Levels(
                                        db.Values.Where(x =>
                                                long.Parse(
                                                    x.Cells[ValueId].Value.TextValue
                                                )
                                                == id + 1
                                            )
                                            .Select(x =>
                                                long.Parse(
                                                    x.Cells[ValueId].Value.TextValue
                                                )
                                            ),
                                        inner => inner.Where(x => x < 0)
                                    ),
                                (id, below) => below
                            )
                            .AsQueryable()
                )
        );
    }

    /// <summary>
    /// The row of <c>Values</c> whose id is 1, at level 0.
    /// </summary>
    private IQueryable<Hop> Seed(ExecutionTables db)
    {
        return db
            .Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
            .Select(x => new Hop(long.Parse(x.Cells[ValueId].Value.TextValue), 0));
    }

    /// <summary>
    /// A walk that never ends, each level the one before it a level deeper, within
    /// the maximum depth.
    /// </summary>
    private IQueryable<Hop> Endless(ExecutionTables db, long maxDepth)
    {
        return MarkerCalls.WithRecursive(
            Seed(db),
            level => level.Select(x => new Hop(x.Id, x.Depth + 1)),
            maxDepth
        );
    }

    private IEnumerable<Hop> InMemoryEndless(ExecutionTables db, long maxDepth)
    {
        return RecursionReference.Levels(
            Seed(db),
            level => level.Select(x => new Hop(x.Id, x.Depth + 1)),
            maxDepth
        );
    }
}
