using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A node of a walk: the id reached, the level it was reached at and the path to it.
/// </summary>
internal sealed record WalkNode(long Id, long Depth, string Path);

/// <summary>
/// Recursive queries over stored tables, written with the translator's marker
/// <c>PostgreSqlRecursion.WithRecursive</c>, which throws when called: the provider
/// keeps the call, rewrites the cells read in its seed and step into column
/// properties and reads the rows of every level, so that the query runs as one
/// statement and returns what running the step level by level over the enumerated
/// rows returns, rows of the tables included; a row deeper than the maximum depth
/// fails the statement, and the steps PostgreSQL forbids are refused before any
/// statement runs.
/// </summary>
public sealed record RecursionTests : IClassFixture<ExecutionFixture>
{
    private static readonly long[] Starts = [1, 4, 91];

    private readonly ExecutionFixture _fixture;

    public RecursionTests(ExecutionFixture fixture)
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

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ComputesLevelsFromStoredRows(int levels)
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new WalkNode(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        x.Cells[Text].Value.TextValue
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < levels)
                        .Select(x => new WalkNode(x.Id * 2, x.Depth + 1, x.Path + "/" + x.Id))
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void WalksStoredRowsThroughACycleUpToADepth(int depth)
    {
        AssertWalk(
            db =>
                db.Parents.Select(x => new WalkNode(
                    long.Parse(x.Cells[ParentId].Value.TextValue),
                    0,
                    x.Cells[ParentKey].Value.TextValue
                )),
            db =>
                level =>
                    db.Children.Join(
                        level.Where(x => x.Depth < depth),
                        x =>
                            string.IsNullOrEmpty(x.Cells[ChildNumber].Value.TextValue)
                                ? null
                                : long.Parse(x.Cells[ChildNumber].Value.TextValue),
                        x => (long?)x.Id,
                        (child, node) =>
                            new WalkNode(
                                long.Parse(child.Cells[ChildId].Value.TextValue),
                                node.Depth + 1,
                                node.Path + ">" + child.Cells[ChildLabel].Value.TextValue
                            )
                    )
        );
    }

    [Fact]
    public void WalksStoredRowsUntilALevelIsEmpty()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                        level =>
                            db.Parents.Join(
                                level,
                                x =>
                                    string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                                        ? null
                                        :
                                            long.Parse(x.Cells[ParentNumber].Value.TextValue),
                                x =>
                                    (long?)long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1,
                                (parent, before) => parent
                            )
                    )
                    .Select(x => new ValueTuple<IRow, string>(x, x.Cells[ParentKey].Value.TextValue)),
            db =>
                Levels(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                        level =>
                            db.Parents.Join(
                                level,
                                x =>
                                    string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                                        ? null
                                        :
                                            long.Parse(x.Cells[ParentNumber].Value.TextValue),
                                x =>
                                    (long?)long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1,
                                (parent, before) => parent
                            )
                    )
                    .Select(x => new ValueTuple<IRow, string>(x, x.Cells[ParentKey].Value.TextValue))
        );
    }

    [Fact]
    public void ReadsTheStoredRowsOfEveryLevel()
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
                                (long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1).ToString(),
                            (parent, before) => parent
                        )
                ),
            db =>
                Levels(
                    db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                    level =>
                        db.Parents.Join(
                            level,
                            x => x.Cells[ParentNumber].Value.TextValue,
                            x =>
                                (long.Parse(x.Cells[ParentNumber].Value.TextValue) - 1).ToString(),
                            (parent, before) => parent
                        )
                )
        );
    }

    [Fact]
    public void SeedsAWalkWithAnInMemorySequence()
    {
        AssertWalk(
            db =>
                db.Values.Provider.CreateQuery<WalkNode>(
                    Starts.AsQueryable().Select(x => new WalkNode(x, 0, "start")).Expression
                ),
            db =>
                level =>
                    db.Values.Join(
                        level.Where(x => x.Depth < 3),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        x => x.Id + 1,
                        (row, node) =>
                            new WalkNode(
                                long.Parse(row.Cells[ValueId].Value.TextValue),
                                node.Depth + 1,
                                node.Path + "," + row.Cells[Text].Value.TextValue
                            )
                    )
        );
    }

    [Fact]
    public void OrdersFiltersAndPagesTheRowsOfAWalk()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 4).Select(x => new WalkNode(x.Id + 3, x.Depth + 1, x.Path));

        Enumeration.AssertSameOrder(
            db =>
                MarkerCalls.WithRecursive(Seed(db), step)
                    .Where(x => x.Id % 2 == 0)
                    .OrderByDescending(x => x.Depth)
                    .ThenBy(x => x.Id)
                    .Skip(1)
                    .Take(6)
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)),
            db =>
                Levels(Seed(db), step.Compile())
                    .Where(x => x.Id % 2 == 0)
                    .OrderByDescending(x => x.Depth)
                    .ThenBy(x => x.Id)
                    .Skip(1)
                    .Take(6)
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path))
        );
    }

    [Fact]
    public void ReadsAnElementAndAggregatesOfAWalk()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 6).Select(x => new WalkNode(x.Id * 3, x.Depth + 1, ""));

        Enumeration.AssertSameValue(
            db => MarkerCalls.WithRecursive(Seed(db), step).Max(x => x.Id),
            db => Levels(Seed(db), step.Compile()).Max(x => x.Id)
        );
        Enumeration.AssertSameValue(
            db => MarkerCalls.WithRecursive(Seed(db), step).Count(x => x.Depth > 2),
            db => Levels(Seed(db), step.Compile()).Count(x => x.Depth > 2)
        );
        Enumeration.AssertSameValue(
            db =>
                MarkerCalls.WithRecursive(Seed(db), step)
                    .OrderByDescending(x => x.Id)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
                    .First(),
            db =>
                Levels(Seed(db), step.Compile())
                    .OrderByDescending(x => x.Id)
                    .Select(x => new ValueTuple<long, long>(x.Id, x.Depth))
                    .First()
        );
    }

    [Fact]
    public void GroupsTheRowsOfAWalk()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 3).Select(x => new WalkNode(x.Id + 1, x.Depth + 1, x.Path));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(Seed(db), step)
                    .GroupBy(x => x.Depth)
                    .Select(x => new ValueTuple<long, int, long>(x.Key, x.Count(), x.Sum(n => n.Id))),
            db =>
                Levels(Seed(db), step.Compile())
                    .GroupBy(x => x.Depth)
                    .Select(x => new ValueTuple<long, int, long>(x.Key, x.Count(), x.Sum(n => n.Id)))
        );
    }

    [Fact]
    public void FiltersRowsByACapturedWalk()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 2).Select(x => new WalkNode(x.Id * 3, x.Depth + 1, ""));

        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<WalkNode> walk = MarkerCalls.WithRecursive(Seed(db), step);
                return db.Values.Where(row =>
                        walk.Any(x => x.Id == long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
            {
                long[] reached = [.. Levels(Seed(db), step.Compile()).Select(x => x.Id)];
                return db.Values.Where(row =>
                        reached.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            }
        );
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    public void ReturnsEveryLevelWithinTheMaximumDepth(long maxDepth)
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 3).Select(x => new WalkNode(x.Id + 1, x.Depth + 1, ""));

        Enumeration.AssertSameElements(
            db => MarkerCalls.WithRecursive(Seed(db), step, maxDepth),
            db => Levels(Seed(db), step.Compile())
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void FailsWhereARowIsDeeperThanTheMaximumDepth(long maxDepth)
    {
        using RecordingConnection connection = new RecordingConnection(_fixture.Connection);
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        PostgresException exception = Assert.Throws<PostgresException>(() =>
            MarkerCalls.WithRecursive(
                    db.Parents.Select(x => new WalkNode(
                        long.Parse(x.Cells[ParentId].Value.TextValue),
                        0,
                        ""
                    )),
                    level =>
                        db.Children.Join(
                            level,
                            x =>
                                string.IsNullOrEmpty(x.Cells[ChildNumber].Value.TextValue)
                                    ? null
                                    : long.Parse(x.Cells[ChildNumber].Value.TextValue),
                            x => (long?)x.Id,
                            (child, node) =>
                                new WalkNode(
                                    long.Parse(child.Cells[ChildId].Value.TextValue),
                                    node.Depth + 1,
                                    ""
                                )
                        ),
                    maxDepth
                )
                .Take(1)
                .ToList()
        );

        Assert.Contains(
            $"WITH RECURSIVE: a row deeper than the maximum depth of {maxDepth}",
            exception.MessageText,
            StringComparison.Ordinal
        );
        _ = Assert.Single(connection.Statements);
    }

    [Fact]
    public void KeepsARecursionCallWhenInliningCapturedQueries()
    {
        IQueryable<IRow> parents = ExecutionTables.Stored(_fixture, _fixture.Connection).Parents;
        IQueryable<IRow> walk = MarkerCalls.WithRecursive(
            parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                parents.Join(
                    level,
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue,
                    (parent, before) => parent
                )
        );
        IQueryable<bool> query = parents.Select(x => walk.Any());

        Expression inlined = new CapturedQueries().Visit(query.Expression)!;

        MethodCallExpression recursion = Assert.Single(new MarkerCallFinder().In(inlined));
        Assert.Equal(nameof(PostgreSqlRecursion.WithRecursive), recursion.Method.Name);
        Assert.Equal(2, new TableConstantCounter().In(recursion));
    }

    [Fact]
    public void RefusesAStepReadingTheLevelTwice()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Seed(db),
                level =>
                    level.Join(
                        level,
                        x => x.Id,
                        x => x.Id - 1,
                        (one, other) => new WalkNode(other.Id, one.Depth + 1, "")
                    )
            )
        );
    }

    [Fact]
    public void RefusesAStepReadingTheLevelOnTheOptionalSideOfAJoin()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Seed(db),
                level =>
                    db.Values.GroupJoin(
                            level,
                            x => long.Parse(x.Cells[ValueId].Value.TextValue),
                            x => x.Id + 1,
                            (row, nodes) => new { Row = row, Nodes = nodes }
                        )
                        .SelectMany(
                            x => x.Nodes.DefaultIfEmpty(),
                            (x, node) =>
                                new WalkNode(
                                    long.Parse(x.Row.Cells[ValueId].Value.TextValue),
                                    node == null ? 0 : node.Depth + 1,
                                    ""
                                )
                        )
            )
        );
    }

    [Fact]
    public void RefusesAStepReadingTheLevelInASubquery()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Seed(db),
                level =>
                    db.Values.Where(row =>
                            level.Any(x =>
                                x.Id + 1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                            )
                        )
                        .Select(row => new WalkNode(
                            long.Parse(row.Cells[ValueId].Value.TextValue),
                            1,
                            ""
                        ))
            )
        );
    }

    [Fact]
    public void RefusesAStepThatGroups()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Seed(db),
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .GroupBy(x => x.Depth)
                        .Select(x => new WalkNode(x.Max(n => n.Id), x.Key + 1, ""))
            )
        );
    }

    // The level a step reads holds rows of the table of its seed, so the count of
    // the cells of its rows is the count of the columns of that table.
    [Fact]
    public void ReadsTheCountOfTheCellsOfTheRowsOfTheLevel()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
                        level =>
                            db.Parents.Join(
                                level.Where(x => x.Cells.Count > 5),
                                x => x.Cells[ParentNumber].Value.TextValue,
                                x => x.Cells[ParentId].Value.TextValue,
                                (parent, before) => parent
                            )
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue),
            db =>
                db.Parents.Where(x =>
                        x.Cells[ParentId].Value.TextValue == "2" && x.Cells.Count <= 5
                    )
                    .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void CountsTheCellsOfTheStoredRowsOfAStep()
    {
        Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>> step = level =>
            level.Where(x => x.Depth < 2).Select(x => new WalkNode(x.Id, x.Depth + 1, x.Path));

        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                        db.Parents.Select(x => new WalkNode(
                            long.Parse(x.Cells[ParentId].Value.TextValue),
                            x.Cells.Count,
                            x.Cells.Keys.Count().ToString()
                        )),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 6),
                                x => long.Parse(x.Cells[ChildId].Value.TextValue),
                                x => x.Id,
                                (child, node) =>
                                    new WalkNode(node.Id + 100, node.Depth + child.Cells.Count, "")
                            )
                    )
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)),
            db =>
                Levels(
                        db.Parents.Select(x => new WalkNode(
                            long.Parse(x.Cells[ParentId].Value.TextValue),
                            x.Cells.Count,
                            x.Cells.Keys.Count().ToString()
                        )),
                        level =>
                            db.Children.Join(
                                level.Where(x => x.Depth < 6),
                                x => long.Parse(x.Cells[ChildId].Value.TextValue),
                                x => x.Id,
                                (child, node) =>
                                    new WalkNode(node.Id + 100, node.Depth + child.Cells.Count, "")
                            )
                    )
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path))
        );
    }

    [Fact]
    public void RefusesARecursionOverTablesOfTwoConnections()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> children = ExecutionTables.Stored(_fixture, other).Children;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            MarkerCalls.WithRecursive(
                    Seed(ExecutionTables.Stored(_fixture, one)),
                    level =>
                        children.Join(
                            level.Where(x => x.Depth < 2),
                            x => long.Parse(x.Cells[ChildId].Value.TextValue),
                            x => x.Id,
                            (child, node) => new WalkNode(node.Id, node.Depth + 1, "")
                        )
                )
                .ToList()
        );

        Assert.Equal(
            "The query operator WithRecursive reads tables of more than one connection, "
                + "so it cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    /// <summary>
    /// The rows of every level, the seed's first, each level the step over the one
    /// before, up to the first that is empty.
    /// </summary>
    private static List<T> Levels<T>(IQueryable<T> seed, Func<IQueryable<T>, IQueryable<T>> step)
    {
        List<T> rows = [];
        T[] level = [.. seed];
        while (level.Length > 0)
        {
            rows.AddRange(level);
            level = [.. step(level.AsQueryable())];
        }
        return rows;
    }

    /// <summary>
    /// The values of the <c>Values</c> table without NULL cells as level 0.
    /// </summary>
    private IQueryable<WalkNode> Seed(ExecutionTables db)
    {
        return db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
            .Select(x => new WalkNode(
                long.Parse(x.Cells[ValueId].Value.TextValue),
                0,
                x.Cells[Text].Value.TextValue
            ));
    }

    /// <summary>
    /// The walk, translated, returns what running its step level by level over the
    /// enumerated rows returns.
    /// </summary>
    private void AssertWalk(
        Func<ExecutionTables, IQueryable<WalkNode>> seed,
        Func<ExecutionTables, Expression<Func<IQueryable<WalkNode>, IQueryable<WalkNode>>>> step
    )
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(seed(db), step(db))
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)),
            db =>
                Levels(seed(db), step(db).Compile())
                    .Select(x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path))
        );
    }

    /// <summary>
    /// The calls of PostgreSQL markers in a tree.
    /// </summary>
    private sealed class MarkerCallFinder : ExpressionVisitor
    {
        private readonly List<MethodCallExpression> _calls = [];

        public IReadOnlyList<MethodCallExpression> In(Expression expression)
        {
            _ = Visit(expression);
            return _calls;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryElement.IsMarker(node))
            {
                _calls.Add(node);
            }
            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// The stored tables a tree holds as constants.
    /// </summary>
    private sealed class TableConstantCounter : ExpressionVisitor
    {
        private int _count;

        public int In(Expression expression)
        {
            _ = Visit(expression);
            return _count;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is PostgreSqlStoredTableDataSet)
            {
                _count++;
            }
            return node;
        }
    }
}
