using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Queries of the PostgreSQL markers captured by the query that reads them — in a
/// local, a field, a property, returned by a static, instance or generic method or
/// an extension, converted, or composed further — and marker calls built by the
/// compiler inside an expression tree rather than with <c>Expression.Call</c>: the
/// captured query is inlined and the marker kept, so that the query runs as one
/// statement and returns what the computation in memory returns.
/// </summary>
public sealed record CapturedMarkerTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    private IQueryable<ValueTuple<long, long>>? _rankedField;

    public CapturedMarkerTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IQueryable<ValueTuple<long, long>>? RankedProperty { get; set; }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void FiltersRowsByAWindowCapturedInAField()
    {
        AssertFirstRanked(db =>
        {
            _rankedField = Ranked(db);
            return db.Values.Where(row =>
                _rankedField.Any(x =>
                    x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                    && x.Item2 <= 3
                )
            );
        });
    }

    [Fact]
    public void FiltersRowsByAWindowCapturedInAProperty()
    {
        AssertFirstRanked(db =>
        {
            RankedProperty = Ranked(db);
            return db.Values.Where(row =>
                RankedProperty.Any(x =>
                    x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                    && x.Item2 <= 3
                )
            );
        });
    }

    [Fact]
    public void FiltersRowsByAWindowReturnedByAnInstanceMethod()
    {
        AssertFirstRanked(db =>
            db.Values.Where(row =>
                Ranked(db)
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            )
        );
    }

    [Fact]
    public void FiltersRowsByAWindowReturnedByAStaticGenericMethod()
    {
        IColumn id = ValueId;
        AssertFirstRanked(db =>
            db.Values.Where(row =>
                Numbered(
                        db.Values.Where(x => x.Cells[Text].Value.TextValue == "plain"),
                        id
                    )
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            )
        );
    }

    [Fact]
    public void FiltersRowsByAWindowReturnedByAnExtension()
    {
        IColumn id = ValueId;
        AssertFirstRanked(db =>
            db.Values.Where(row =>
                db.Values.Where(x => x.Cells[Text].Value.TextValue == "plain")
                    .NumberedBy(id)
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            )
        );
    }

    [Fact]
    public void FiltersRowsByAWindowCapturedAsAnUntypedQuery()
    {
        AssertFirstRanked(db =>
        {
            IQueryable untyped = Ranked(db);
            return db.Values.Where(row =>
                untyped
                    .Cast<ValueTuple<long, long>>()
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            );
        });
    }

    [Fact]
    public void FiltersRowsByAWindowOfACapturedQuery()
    {
        AssertFirstRanked(db =>
        {
            IQueryable<IRow> values = db.Values.Where(x =>
                x.Cells[Text].Value.TextValue == "plain"
            );
            IColumn id = ValueId;
            Expression<Func<IQueryable<ValueTuple<long, long>>>> ranked = () =>
                values.Over(
                    null,
                    rows => rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
                    null,
                    (x, w) =>
                        new ValueTuple<long, long>(
                            long.Parse(x.Cells[id].Value.TextValue),
                            w.RowNumber()
                        )
                );
            IQueryable<ValueTuple<long, long>> captured = db.Values.Provider.CreateQuery<
                ValueTuple<long, long>
            >(ranked.Body);
            return db.Values.Where(row =>
                captured.Any(x =>
                    x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                    && x.Item2 <= 3
                )
            );
        });
    }

    [Fact]
    public void FiltersRowsByAWindowWrittenInTheQuery()
    {
        AssertFirstRanked(db =>
        {
            IQueryable<IRow> values = db.Values.Where(x =>
                x.Cells[Text].Value.TextValue == "plain"
            );
            IColumn id = ValueId;
            return db.Values.Where(row =>
                values
                    .Over(
                        null,
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
                        null,
                        (x, w) =>
                            new ValueTuple<long, long>(
                                long.Parse(x.Cells[id].Value.TextValue),
                                w.RowNumber()
                            )
                    )
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            );
        });
    }

    [Fact]
    public void FiltersRowsByAWindowCapturedInANestedLambda()
    {
        AssertFirstRanked(db =>
        {
            IQueryable<ValueTuple<long, long>> ranked = Ranked(db);
            return db.Values.Where(row =>
                db.Values.Select(v => long.Parse(v.Cells[ValueId].Value.TextValue))
                    .Where(v => v <= 3)
                    .Any(p =>
                        ranked.Any(x =>
                            x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                            && x.Item2 == p
                        )
                    )
            );
        });
    }

    [Fact]
    public void ReadsAWindowBuiltByTheCompilerWithAFrame()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                Expression<Func<IQueryable<ValueTuple<long, long>>>> query = () =>
                    values.Over(
                        null,
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
                        PostgreSqlFrame.Rows(-1, 1),
                        (x, w) =>
                            new ValueTuple<long, long>(
                                long.Parse(x.Cells[id].Value.TextValue),
                                w.Rows.Sum(row =>
                                    long.Parse(row.Cells[id].Value.TextValue)
                                )
                            )
                    );
                return db.Values.Provider.CreateQuery<ValueTuple<long, long>>(query.Body);
            },
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<long, long>(
                        long.Parse(x.Row.Cells[ValueId].Value.TextValue),
                        x.Window.Rows(-1, 1)
                            .Sum(row => long.Parse(row.Cells[ValueId].Value.TextValue))
                    ))
        );
    }

    [Fact]
    public void ReadsAWindowBuiltByTheCompilerWithAFrameHeldInAVariable()
    {
        PostgreSqlFrame frame = PostgreSqlFrame.Rows(null, 0);

        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                Expression<Func<IQueryable<ValueTuple<long, long>>>> query = () =>
                    values.Over(
                        null,
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
                        frame,
                        (x, w) =>
                            new ValueTuple<long, long>(
                                long.Parse(x.Cells[id].Value.TextValue),
                                w.Rows.LongCount()
                            )
                    );
                return db.Values.Provider.CreateQuery<ValueTuple<long, long>>(query.Body);
            },
            db =>
                InMemoryWindow<IRow>
                    .Over(
                        db.Values,
                        x => null,
                        rows =>
                            rows.OrderBy(x =>
                                long.Parse(x.Cells[ValueId].Value.TextValue)
                            ),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue)
                    )
                    .Select(x => new ValueTuple<long, long>(
                        long.Parse(x.Row.Cells[ValueId].Value.TextValue),
                        x.Window.Rows(null, 0).LongCount()
                    ))
        );
    }

    [Fact]
    public void ReadsARecursionBuiltByTheCompiler()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> parents = db.Parents;
                IColumn id = ParentId;
                Expression<Func<IQueryable<IRow>>> query = () =>
                    parents
                        .Where(x => x.Cells[id].Value.TextValue == "2")
                        .WithRecursive(level =>
                            parents.Join(
                                level,
                                x => x.Cells[id].Value.TextValue,
                                x => x.Cells[id].Value.TextValue == "2" ? "4" : "0",
                                (parent, before) => parent
                            )
                        );
                return db.Values.Provider.CreateQuery<IRow>(query.Body);
            },
            db =>
                db.Parents.Where(x =>
                    x.Cells[ParentId].Value.TextValue == "2"
                    || x.Cells[ParentId].Value.TextValue == "4"
                )
        );
    }

    [Fact]
    public void ReadsARecursionWithADepthBuiltByTheCompilerFromAVariable()
    {
        long depth = 4;

        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                Expression<Func<IQueryable<long>>> query = () =>
                    values
                        .Where(x => x.Cells[id].Value.TextValue == "1")
                        .Select(x => long.Parse(x.Cells[id].Value.TextValue))
                        .WithRecursive(
                            level => level.Where(x => x < 3).Select(x => x + 1),
                            depth
                        );
                return db.Values.Provider.CreateQuery<long>(query.Body);
            },
            db => [1, 2, 3]
        );
    }

    [Fact]
    public void JoinsRowsToACapturedWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = Ranked(db);
                return db.Parents.Join(
                    ranked,
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Item1,
                    (parent, rank) =>
                        new ValueTuple<string, long>(
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Item2
                        )
                );
            },
            db =>
                db.Parents.Join(
                    InMemoryRanked(db),
                    x => long.Parse(x.Cells[ParentId].Value.TextValue),
                    x => x.Item1,
                    (parent, rank) =>
                        new ValueTuple<string, long>(
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Item2
                        )
                )
        );
    }

    [Fact]
    public void FlattensRowsWithTheResultsOfACapturedWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = Ranked(db);
                return db.Parents.SelectMany(
                    x =>
                        ranked.Where(r =>
                            r.Item1 == long.Parse(x.Cells[ParentId].Value.TextValue)
                        ),
                    (parent, rank) =>
                        new ValueTuple<string, long>(
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Item2
                        )
                );
            },
            db =>
            {
                ValueTuple<long, long>[] ranked = [.. InMemoryRanked(db)];
                return db.Parents.SelectMany(
                    x =>
                        ranked.Where(r =>
                            r.Item1 == long.Parse(x.Cells[ParentId].Value.TextValue)
                        ),
                    (parent, rank) =>
                        new ValueTuple<string, long>(
                            parent.Cells[ParentKey].Value.TextValue,
                            rank.Item2
                        )
                );
            }
        );
    }

    [Fact]
    public void ConcatenatesValuesWithTheResultsOfACapturedWindow()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<ValueTuple<long, long>> ranked = Ranked(db);
                return db
                    .Parents.Select(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Concat(ranked.Select(x => x.Item2));
            },
            db =>
                db.Parents.Select(x => long.Parse(x.Cells[ParentId].Value.TextValue))
                    .Concat(InMemoryRanked(db).Select(x => x.Item2))
        );
    }

    [Fact]
    public void CountsACapturedRecursionInAProjection()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> walk = Walk(db);
                return db.Children.Select(x => new ValueTuple<string, int>(
                    x.Cells[ChildId].Value.TextValue,
                    walk.Count(p =>
                        p.Cells[ParentKey].Value.TextValue
                        == x.Cells[ChildKey].Value.TextValue
                    )
                ));
            },
            db =>
            {
                IRow[] walk =
                [
                    .. db.Parents.Where(x =>
                        x.Cells[ParentId].Value.TextValue == "2"
                        || x.Cells[ParentId].Value.TextValue == "4"
                    ),
                ];
                return db.Children.Select(x => new ValueTuple<string, int>(
                    x.Cells[ChildId].Value.TextValue,
                    walk.Count(p =>
                        p.Cells[ParentKey].Value.TextValue
                        == x.Cells[ChildKey].Value.TextValue
                    )
                ));
            }
        );
    }

    [Fact]
    public void WindowsACapturedRecursion()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> walk = Walk(db);
                return db.Values.Provider.CreateQuery<ValueTuple<string, long>>(
                    MarkerCalls.OverCall<IRow, ValueTuple<string, long>>(
                        Expression.Property(
                            Expression.Constant(new Holder<IQueryable<IRow>>(walk)),
                            nameof(Holder<>.Value)
                        ),
                        null,
                        rows =>
                            rows.OrderByDescending(x =>
                                x.Cells[ParentId].Value.TextValue
                            ),
                        null,
                        (x, w) =>
                            new ValueTuple<string, long>(
                                x.Cells[ParentId].Value.TextValue,
                                w.RowNumber()
                            )
                    )
                );
            },
            db => [("4", 1), ("2", 2)]
        );
    }

    [Fact]
    public void FiltersRowsByAWindowCapturedAsAnEnumerable()
    {
        AssertFirstRanked(db =>
        {
            IEnumerable<ValueTuple<long, long>> ranked = Ranked(db);
            return db.Values.Where(row =>
                ranked.Any(x =>
                    x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                    && x.Item2 <= 3
                )
            );
        });
    }

    [Fact]
    public void FiltersRowsByIdsOfAStoredQueryCapturedAsAnEnumerable()
    {
        Enumeration.AssertSameElements(db =>
        {
            IEnumerable<long> ids = db.Parents.Select(x =>
                long.Parse(x.Cells[ParentId].Value.TextValue)
            );
            return db
                .Values.Where(row =>
                    ids.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[Text].Value.TextValue);
        });
    }

    [Fact]
    public void FiltersRowsByAWindowReturnedByAnInvokedDelegate()
    {
        AssertFirstRanked(db =>
        {
            Func<IQueryable<ValueTuple<long, long>>> ranked = () => Ranked(db);
            return db.Values.Where(row =>
                ranked()
                    .Any(x =>
                        x.Item1 == long.Parse(row.Cells[ValueId].Value.TextValue)
                        && x.Item2 <= 3
                    )
            );
        });
    }

    [Fact]
    public void FiltersRowsByATableCapturedAsAnEnumerableReadWithoutAsQueryable()
    {
        Enumeration.AssertSameElements(db =>
        {
            IEnumerable<IRow> parents = db.Parents;
            IColumn id = ParentId;
            return db
                .Values.Where(row =>
                    parents.Any(x =>
                        x.Cells[id].Value.TextValue == row.Cells[ValueId].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue);
        });
    }

    [Fact]
    public void FiltersRowsByAQueryOfRowsConvertedToAnEnumerableOfRows()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                IColumn text = Text;
                return db
                    .Values.Where(row =>
                        values
                            .Where(x => x.Cells[text].Value.TextValue == "plain")
                            .AsQueryable()
                            .Any(x =>
                                x.Cells[id].Value.TextValue
                                == row.Cells[id].Value.TextValue
                            )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db =>
                db.Values.Where(x => x.Cells[Text].Value.TextValue == "plain")
                    .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    // The provider keeps the marker in the tree and never calls it: the window is
    // translated where it stands, read as a query again.
    [Fact]
    public void FiltersRowsByAWindowReadAsAQueryableAgain()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                return db
                    .Values.Where(row =>
                        values
                            .Over(null, null, null, (x, w) => x)
                            .AsQueryable()
                            .Any(x =>
                                x.Cells[id].Value.TextValue
                                == row.Cells[id].Value.TextValue
                            )
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue);
            },
            db => db.Values.Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsAWindowWhoseSelectorIsHeldInAVariable()
    {
        Enumeration.AssertSameElements(
            db =>
            {
                IQueryable<IRow> values = db.Values;
                IColumn id = ValueId;
                Expression<Func<IRow, PostgreSqlWindow<IRow>, long>> selector = (x, w) =>
                    (long.Parse(x.Cells[id].Value.TextValue) * 100) + w.RowNumber();
                Expression<Func<IQueryable<long>>> query = () =>
                    values.Over(
                        null,
                        rows =>
                            rows.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
                        null,
                        selector
                    );
                return db.Values.Provider.CreateQuery<long>(query.Body);
            },
            db =>
                db.Values.AsEnumerable()
                    .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                    .Order()
                    .Select((x, index) => (x * 100) + index + 1)
        );
    }

    [Fact]
    public async Task RunsAWindowOnTheConnectionWhileATableIsEnumeratedAsynchronously()
    {
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);
        PostgreSqlStoredTableDataSet parents =
            Assert.IsType<PostgreSqlStoredTableDataSet>(db.Parents);
        List<ValueTuple<string, long>> read = [];

        await foreach (IRow parent in parents)
        {
            string id = parent.Cells[ParentId].Value.TextValue;
            read.Add(
                (
                    id,
                    Ranked(db)
                        .Where(x => x.Item1.ToString() == id)
                        .Select(x => x.Item2)
                        .Sum()
                )
            );
        }

        ExecutionTables enumerated = ExecutionTables.Enumerated(_fixture);
        Assert.Equal(
            enumerated
                .Parents.AsEnumerable()
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .Select(id => new ValueTuple<string, long>(
                    id,
                    InMemoryRanked(enumerated)
                        .Where(x => x.Item1.ToString() == id)
                        .Sum(x => x.Item2)
                ))
                .Order(),
            read.Order()
        );
        Assert.Equal(6, connection.Statements.Count);
    }

    [Fact]
    public async Task RunsAWindowOnTheConnectionWhileAnotherIsEnumeratedAsynchronously()
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);
        PostgreSqlStoredTableDataSet parents =
            Assert.IsType<PostgreSqlStoredTableDataSet>(db.Parents);
        List<long> read = [];

        await foreach (IRow parent in parents)
        {
            foreach (ValueTuple<long, long> rank in Ranked(db).Where(x => x.Item2 == 1))
            {
                read.Add(rank.Item1 + long.Parse(parent.Cells[ParentId].Value.TextValue));
            }
        }

        Assert.Equal(5, read.Count);
    }

    /// <summary>
    /// The text of the values ranked first by their id, filtered by the query that
    /// reads the ranks, as the ranks computed in memory filter them.
    /// </summary>
    private void AssertFirstRanked(Func<ExecutionTables, IQueryable<IRow>> query)
    {
        Enumeration.AssertSameElements(
            db => query(db).Select(x => x.Cells[Text].Value.TextValue),
            db =>
            {
                long[] first =
                [
                    .. InMemoryRanked(db).Where(x => x.Item2 <= 3).Select(x => x.Item1),
                ];
                return db
                    .Values.Where(row =>
                        first.Contains(long.Parse(row.Cells[ValueId].Value.TextValue))
                    )
                    .Select(x => x.Cells[Text].Value.TextValue);
            }
        );
    }

    private IQueryable<ValueTuple<long, long>> Ranked(ExecutionTables db)
    {
        return Numbered(
            db.Values.Where(x => x.Cells[Text].Value.TextValue == "plain"),
            ValueId
        );
    }

    private IEnumerable<ValueTuple<long, long>> InMemoryRanked(ExecutionTables db)
    {
        return db
            .Values.Where(x => x.Cells[Text].Value.TextValue == "plain")
            .AsEnumerable()
            .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
            .Order()
            .Select((id, index) => new ValueTuple<long, long>(id, index + 1));
    }

    private IQueryable<IRow> Walk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(
            db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "2"),
            level =>
                db.Parents.Join(
                    level,
                    x => x.Cells[ParentId].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue == "2" ? "4" : "0",
                    (parent, before) => parent
                )
        );
    }

    /// <summary>
    /// The rows numbered by the column, an id, in its order.
    /// </summary>
    private static IQueryable<ValueTuple<long, long>> Numbered<TRow>(
        IQueryable<TRow> rows,
        IColumn id
    )
        where TRow : IRow
    {
        return MarkerCalls.Over(
            rows,
            null,
            items => items.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
            null,
            (x, w) =>
                new ValueTuple<long, long>(
                    long.Parse(x.Cells[id].Value.TextValue),
                    w.RowNumber()
                )
        );
    }

    /// <summary>
    /// A value read from a member of a constant, as a closure holds a capture.
    /// </summary>
    private sealed record Holder<T>(T Value);
}

/// <summary>
/// A window over rows as an extension, which a query calls inside its lambdas.
/// </summary>
internal static class NumberingExtensions
{
    public static IQueryable<ValueTuple<long, long>> NumberedBy(
        this IQueryable<IRow> rows,
        IColumn id
    )
    {
        return MarkerCalls.Over(
            rows,
            null,
            items => items.OrderBy(x => long.Parse(x.Cells[id].Value.TextValue)),
            null,
            (x, w) =>
                new ValueTuple<long, long>(
                    long.Parse(x.Cells[id].Value.TextValue),
                    w.RowNumber()
                )
        );
    }
}
