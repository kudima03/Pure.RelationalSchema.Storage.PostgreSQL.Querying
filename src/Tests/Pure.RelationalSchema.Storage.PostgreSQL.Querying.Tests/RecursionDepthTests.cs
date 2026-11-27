using System.Linq.Expressions;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A node reached at the end of a path that lists every node it went through.
/// </summary>
internal sealed record PathHop(long Id, string Path);

/// <summary>
/// Walks of <c>PostgreSqlRecursion.WithRecursive</c> through graphs with cycles,
/// diamonds and NULL links, written as in-memory edges the translator writes as
/// <c>VALUES</c> inside the step, ended by a predicate on the level, by the path
/// or by the maximum depth: rows reached twice are kept (<c>UNION ALL</c>), a row
/// exactly at the maximum depth is returned, and a row deeper than it fails the
/// statement with PostgreSQL's error whatever the query around the walk reads; a
/// maximum depth below 0 is refused before any statement runs.
/// </summary>
public sealed record RecursionDepthTests : IClassFixture<ExecutionFixture>
{
    /// <summary>
    /// A cycle 1 → 2 → 3 → 1, a way out of it to 4, which loops to itself, a
    /// diamond 5 → 6, 7 → 8 and an edge from 9 to nowhere in the tables.
    /// </summary>
    private static readonly (long From, long To)[] Edges =
    [
        (1, 2),
        (2, 3),
        (3, 1),
        (3, 4),
        (4, 4),
        (5, 6),
        (5, 7),
        (6, 8),
        (7, 8),
        (9, 500),
    ];

    /// <summary>
    /// Links whose ends may be NULL, which never join.
    /// </summary>
    private static readonly (long? From, long? To)[] NullableEdges =
    [
        (1, 2),
        (2, null),
        (null, 3),
        (null, null),
        (0, 1),
    ];

    private readonly ExecutionFixture _fixture;

    public RecursionDepthTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void WalksACycleOfInMemoryEdgesUpToALevel(int levels)
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id == 1),
            level =>
                level
                    .Where(x => x.Depth < levels)
                    .Join(
                        Edges,
                        x => x.Id,
                        x => x.From,
                        (x, edge) => new Hop(edge.To, x.Depth + 1)
                    )
        );
    }

    [Fact]
    public void KeepsTheNodesADiamondReachesTwice()
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id == 5 || x.Id == 6),
            level =>
                level.Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new Hop(edge.To, x.Depth + 1)
                )
        );
    }

    [Fact]
    public void WalksEveryNodeToTheEndOfItsEdges()
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id > 4),
            level =>
                level.Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new Hop(edge.To, x.Depth + 1)
                )
        );
    }

    [Fact]
    public void EndsAWalkThroughACycleByItsPath()
    {
        Expression<Func<IQueryable<PathHop>, IQueryable<PathHop>>> step = level =>
            level
                .Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new { Hop = x, Edge = edge }
                )
                .Where(x => !x.Hop.Path.Contains("," + x.Edge.To + ","))
                .Select(x => new PathHop(x.Edge.To, x.Hop.Path + x.Edge.To + ","));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Paths(db), step)
                    .Select(x => new ValueTuple<long, string>(x.Id, x.Path)),
            db =>
                RecursionReference
                    .Levels(Paths(db), step.Compile())
                    .Select(x => new ValueTuple<long, string>(x.Id, x.Path))
        );
    }

    [Fact]
    public void EndsAWalkThroughACycleByItsPathWithinAMaximumDepth()
    {
        Expression<Func<IQueryable<PathHop>, IQueryable<PathHop>>> step = level =>
            level
                .Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new { Hop = x, Edge = edge }
                )
                .Where(x => !x.Hop.Path.Contains("," + x.Edge.To + ","))
                .Select(x => new PathHop(x.Edge.To, x.Hop.Path + x.Edge.To + ","));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Paths(db), step, 4)
                    .Select(x => new ValueTuple<long, string>(x.Id, x.Path)),
            db =>
                RecursionReference
                    .Levels(Paths(db), step.Compile(), 4)
                    .Select(x => new ValueTuple<long, string>(x.Id, x.Path))
        );
    }

    [Fact]
    public void NeverJoinsTheNullEndsOfInMemoryLinks()
    {
        Expression<Func<IQueryable<LinkHop>, IQueryable<LinkHop>>> step = level =>
            level
                .Where(x => x.Depth < 4)
                .Join(
                    NullableEdges,
                    x => x.Link,
                    x => x.From,
                    (x, edge) => new LinkHop(x.Id, edge.To, x.Depth + 1)
                );

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Links(db), step)
                    .Select(x => new ValueTuple<long, long?, long>(
                        x.Id,
                        x.Link,
                        x.Depth
                    )),
            db =>
                RecursionReference
                    .Levels(Links(db), step.Compile())
                    .Select(x => new ValueTuple<long, long?, long>(x.Id, x.Link, x.Depth))
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void ReturnsTheRowsExactlyAtTheMaximumDepth(long maxDepth)
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id == 1),
            level =>
                level
                    .Where(x => x.Depth < maxDepth)
                    .Join(
                        Edges,
                        x => x.Id,
                        x => x.From,
                        (x, edge) => new Hop(edge.To, x.Depth + 1)
                    ),
            maxDepth
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void FailsAWalkThroughACycleDeeperThanTheMaximumDepth(long maxDepth)
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id == 1),
            level =>
                level.Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new Hop(edge.To, x.Depth + 1)
                ),
            maxDepth
        );
        AssertDeeperThan(
            maxDepth,
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).Where(x => x.Id == 1),
                        level =>
                            level.Join(
                                Edges,
                                x => x.Id,
                                x => x.From,
                                (x, edge) => new Hop(edge.To, x.Depth + 1)
                            ),
                        maxDepth
                    )
                    .ToList()
        );
    }

    [Fact]
    public void ReturnsTheSeedWithinAMaximumDepthOfZeroWhereTheStepYieldsNothing()
    {
        AssertWalk(
            Nodes,
            level =>
                level.Where(x => x.Id > 1000).Select(x => new Hop(x.Id, x.Depth + 1)),
            0
        );
    }

    [Fact]
    public void FailsAnyStepRowWithinAMaximumDepthOfZero()
    {
        AssertWalk(
            Nodes,
            level => level.Where(x => x.Id == 13).Select(x => new Hop(x.Id, x.Depth + 1)),
            0
        );
    }

    [Fact]
    public void FailsAStepReturningItsLevelWithinAMaximumDepth()
    {
        AssertWalk(Nodes, level => level, 2);
    }

    [Fact]
    public void ReturnsNothingForAnEmptySeedWithinAMaximumDepth()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        Assert.Empty(
            MarkerCalls
                .WithRecursive(Nodes(db).Where(x => x.Id > 1000), level => level, 0)
                .ToList()
        );
        _ = Assert.Single(connection.Statements);
    }

    [Fact]
    public void WalksWithTheLargestMaximumDepth()
    {
        AssertWalk(
            db => Nodes(db).Where(x => x.Id == 1),
            level =>
                level
                    .Where(x => x.Depth < 8)
                    .Join(
                        Edges,
                        x => x.Id,
                        x => x.From,
                        (x, edge) => new Hop(edge.To, x.Depth + 1)
                    ),
            long.MaxValue
        );
    }

    [Fact]
    public void FailsAWalkDeeperThanTheMaximumDepthWhateverTheQueryReads()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level.Join(
                Edges,
                x => x.Id,
                x => x.From,
                (x, edge) => new Hop(edge.To, x.Depth + 1)
            );

        AssertDeeperThan(2, db => Cycle(db, step, 2).Take(1).ToList());
        AssertDeeperThan(2, db => Cycle(db, step, 2).Any());
        AssertDeeperThan(2, db => Cycle(db, step, 2).Count());
        AssertDeeperThan(2, db => Cycle(db, step, 2).OrderBy(x => x.Id).First());
        AssertDeeperThan(2, db => Cycle(db, step, 2).Where(x => x.Depth == 0).ToList());
        AssertDeeperThan(
            2,
            db => Cycle(db, step, 2).Select(x => x.Id).Distinct().ToList()
        );
    }

    [Fact]
    public void FailsARowFilteredByAWalkDeeperThanTheMaximumDepth()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level.Join(
                Edges,
                x => x.Id,
                x => x.From,
                (x, edge) => new Hop(edge.To, x.Depth + 1)
            );

        AssertDeeperThan(
            3,
            db =>
            {
                IQueryable<Hop> walk = Cycle(db, step, 3);
                return db
                    .Values.Where(row =>
                        walk.Any(x =>
                            x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                        )
                    )
                    .ToList();
            }
        );
    }

    [Fact]
    public void WalksStoredRowsWithinAMaximumDepth()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
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
                        ),
                    2
                ),
            db =>
                RecursionReference.Levels(
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
                        ),
                    2
                )
        );
    }

    [Fact]
    public void FailsAWalkOfStoredRowsDeeperThanTheMaximumDepth()
    {
        AssertDeeperThan(
            1,
            db =>
                MarkerCalls
                    .WithRecursive(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                        level =>
                            db.Parents.Join(
                                level,
                                x => x.Cells[ParentNumber].Value.TextValue,
                                x =>
                                    (
                                        long.Parse(x.Cells[ParentNumber].Value.TextValue)
                                        - 1
                                    ).ToString(),
                                (parent, before) => parent
                            ),
                        1
                    )
                    .ToList()
        );
    }

    [Fact]
    public void WalksScalarsWithinAMaximumDepth()
    {
        Expression<Func<IQueryable<long>, IQueryable<long>>> step = level =>
            level.Where(x => x < 50).Select(x => x * 2);

        Enumeration.AssertSameElements(
            db => MarkerCalls.WithRecursive(Nodes(db).Select(x => x.Id), step, 6),
            db =>
                RecursionReference.Levels(Nodes(db).Select(x => x.Id), step.Compile(), 6)
        );
    }

    [Fact]
    public void FailsAWalkOfScalarsDeeperThanTheMaximumDepth()
    {
        AssertDeeperThan(
            2,
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).Select(x => x.Id),
                        level => level.Where(x => x < 50).Select(x => x * 2),
                        2
                    )
                    .ToList()
        );
    }

    [Fact]
    public void WalksAGroupedSeedWithinAMaximumDepth()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level.Where(x => x.Id < 20).Select(x => new Hop(x.Id * 3, x.Depth + 1));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db)
                            .GroupBy(x => x.Id % 3)
                            .Select(x => new Hop(x.Count(), 0)),
                        step,
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db)
                            .GroupBy(x => x.Id % 3)
                            .Select(x => new Hop(x.Count(), 0)),
                        step.Compile(),
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void WalksAPagedSeedAndADistinctStepWithinAMaximumDepth()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 3)
                .Select(x => new Hop(x.Id / 2, x.Depth + 1))
                .Distinct();

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).OrderByDescending(x => x.Id).Take(4),
                        step,
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db).OrderByDescending(x => x.Id).Take(4),
                        step.Compile(),
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void ConcatenatesTwoWalksWithinMaximumDepths()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 2)
                .Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new Hop(edge.To, x.Depth + 1)
                );

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Nodes(db).Where(x => x.Id == 1), step, 2)
                    .Concat(
                        MarkerCalls.WithRecursive(
                            Nodes(db).Where(x => x.Id == 5),
                            step,
                            3
                        )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(Nodes(db).Where(x => x.Id == 1), step.Compile(), 2)
                    .Concat(
                        RecursionReference.Levels(
                            Nodes(db).Where(x => x.Id == 5),
                            step.Compile(),
                            3
                        )
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void GroupsTheRowsOfAWalkWithinAMaximumDepth()
    {
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step = level =>
            level
                .Where(x => x.Depth < 5)
                .Join(
                    Edges,
                    x => x.Id,
                    x => x.From,
                    (x, edge) => new Hop(edge.To, x.Depth + 1)
                );

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(Nodes(db).Where(x => x.Id < 4), step, 5)
                    .GroupBy(x => x.Id)
                    .Select(x => new ValueTuple<long, int, long>(
                        x.Key,
                        x.Count(),
                        x.Min(n => n.Depth)
                    )),
            db =>
                RecursionReference
                    .Levels(Nodes(db).Where(x => x.Id < 4), step.Compile(), 5)
                    .GroupBy(x => x.Id)
                    .Select(x => new ValueTuple<long, int, long>(
                        x.Key,
                        x.Count(),
                        x.Min(n => n.Depth)
                    ))
        );
    }

    [Fact]
    public void WalksAStoredCycleWithinAMaximumDepth()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        Nodes(db).Where(x => x.Id < 3),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 4),
                                x =>
                                    string.IsNullOrEmpty(
                                        x.Cells[ChildNumber].Value.TextValue
                                    )
                                        ? null
                                        : long.Parse(
                                            x.Cells[ChildNumber].Value.TextValue
                                        ),
                                x => (long?)x.Id,
                                (child, hop) =>
                                    new Hop(
                                        long.Parse(child.Cells[ChildId].Value.TextValue),
                                        hop.Depth + 1
                                    )
                            ),
                        4
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        Nodes(db).Where(x => x.Id < 3),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 4),
                                x =>
                                    string.IsNullOrEmpty(
                                        x.Cells[ChildNumber].Value.TextValue
                                    )
                                        ? null
                                        : long.Parse(
                                            x.Cells[ChildNumber].Value.TextValue
                                        ),
                                x => (long?)x.Id,
                                (child, hop) =>
                                    new Hop(
                                        long.Parse(child.Cells[ChildId].Value.TextValue),
                                        hop.Depth + 1
                                    )
                            ),
                        4
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Fact]
    public void WalksAnInMemorySeedThroughStoredRowsWithinAMaximumDepth()
    {
        long[] starts = [0, 1, 0];

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        db.Values.Provider.CreateQuery<Hop>(
                            starts.AsQueryable().Select(x => new Hop(x, 0)).Expression
                        ),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 3),
                                x =>
                                    string.IsNullOrEmpty(
                                        x.Cells[ChildNumber].Value.TextValue
                                    )
                                        ? null
                                        : long.Parse(
                                            x.Cells[ChildNumber].Value.TextValue
                                        ),
                                x => (long?)x.Id,
                                (child, hop) =>
                                    new Hop(
                                        long.Parse(child.Cells[ChildId].Value.TextValue)
                                            % 2,
                                        hop.Depth + 1
                                    )
                            ),
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        starts.Select(x => new Hop(x, 0)),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 3),
                                x =>
                                    string.IsNullOrEmpty(
                                        x.Cells[ChildNumber].Value.TextValue
                                    )
                                        ? null
                                        : long.Parse(
                                            x.Cells[ChildNumber].Value.TextValue
                                        ),
                                x => (long?)x.Id,
                                (child, hop) =>
                                    new Hop(
                                        long.Parse(child.Cells[ChildId].Value.TextValue)
                                            % 2,
                                        hop.Depth + 1
                                    )
                            ),
                        3
                    )
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void RefusesAMaximumDepthBelowZero(long maxDepth)
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Nodes(db),
                level => level.Where(x => x.Id < 0),
                maxDepth
            )
        );

        Assert.Equal(
            "The maximum depth of WITH RECURSIVE is a constant of 0 or more.",
            exception.InnerException?.Message
        );
    }

    /// <summary>
    /// The ids of the <c>Values</c> table without NULL cells, as level 0.
    /// </summary>
    private IQueryable<Hop> Nodes(ExecutionTables db)
    {
        return db
            .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
            .Select(x => new Hop(long.Parse(x.Cells[ValueId].Value.TextValue), 0));
    }

    /// <summary>
    /// Paths that start at the nodes 1 and 5.
    /// </summary>
    private IQueryable<PathHop> Paths(ExecutionTables db)
    {
        return Nodes(db)
            .Where(x => x.Id == 1 || x.Id == 5)
            .Select(x => new PathHop(x.Id, "," + x.Id + ","));
    }

    /// <summary>
    /// The parents and the nullable numbers they link to.
    /// </summary>
    private IQueryable<LinkHop> Links(ExecutionTables db)
    {
        return db.Parents.Select(x => new LinkHop(
            long.Parse(x.Cells[ParentId].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                ? null
                : long.Parse(x.Cells[ParentNumber].Value.TextValue),
            0
        ));
    }

    private IQueryable<Hop> Cycle(
        ExecutionTables db,
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step,
        long maxDepth
    )
    {
        return MarkerCalls.WithRecursive(Nodes(db).Where(x => x.Id == 1), step, maxDepth);
    }

    /// <summary>
    /// The walk, translated, returns what running its step level by level over the
    /// enumerated rows returns, or fails as it does.
    /// </summary>
    private void AssertWalk(
        Func<ExecutionTables, IQueryable<Hop>> seed,
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step
    )
    {
        Func<IQueryable<Hop>, IQueryable<Hop>> compiled = step.Compile();
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(seed(db), step)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(seed(db), compiled)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    private void AssertWalk(
        Func<ExecutionTables, IQueryable<Hop>> seed,
        Expression<Func<IQueryable<Hop>, IQueryable<Hop>>> step,
        long maxDepth
    )
    {
        Func<IQueryable<Hop>, IQueryable<Hop>> compiled = step.Compile();
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(seed(db), step, maxDepth)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth)),
            db =>
                RecursionReference
                    .Levels(seed(db), compiled, maxDepth)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
        );
    }

    /// <summary>
    /// The query fails in its one statement with PostgreSQL's error for a row deeper
    /// than the maximum depth.
    /// </summary>
    private void AssertDeeperThan(long maxDepth, Func<ExecutionTables, object> query)
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        PostgresException exception = Assert.Throws<PostgresException>(() => query(db));

        Assert.Equal(
            "invalid input syntax for type bigint: \""
                + RecursionReference.DeeperThan(maxDepth)
                + "\"",
            exception.MessageText
        );
        _ = Assert.Single(connection.Statements);
    }
}
