using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Calls of query operators of a library's own (issue #98): a static method that
/// returns <c>source.Provider.CreateQuery(Call(method, source.Expression))</c>, the
/// query whose tree is the call itself, or one that refuses any provider but LINQ
/// to Objects with <see cref="NotSupportedException"/>. The provider keeps such a
/// call in the tree, which the translator refuses, so the query ends refused
/// rather than evaluating the call again without end, wherever the call is: the
/// whole query, a source of an operator, or a subquery in a lambda. Two operators
/// that call each other, one whose tree holds two calls of itself, a query whose
/// tree is a conversion of itself and a property whose query is a read of that
/// property end the same way. A method returning another query, even one that
/// calls itself on other arguments, is still inlined and translated.
/// </summary>
public sealed record QueryOperatorCallTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public QueryOperatorCallTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItself()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Echoed().Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsTheWholeQuery()
    {
        Enumeration.AssertNotSupported(db => db.Parents.Echoed());
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfInsideALambda()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Echoed()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesNestedCallsOfAnOperatorWhoseQueryIsTheCallItself()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Echoed()
                .Where(x => x.Cells[ParentKey].Value.TextValue != "")
                .Echoed()
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsTheInnerSourceOfAJoin()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Join(
                db.Children.Echoed(),
                x => x.Cells[ParentKey].Value.TextValue,
                x => x.Cells[ChildKey].Value.TextValue,
                (parent, child) => new ValueTuple<string, string>(
                    parent.Cells[ParentId].Value.TextValue,
                    child.Cells[ChildId].Value.TextValue
                )
            )
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsTheOuterSourceOfAJoin()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Echoed()
                .Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) => new ValueTuple<string, string>(
                        parent.Cells[ParentId].Value.TextValue,
                        child.Cells[ChildId].Value.TextValue
                    )
                )
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsTheInnerSourceOfAGroupJoin()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.GroupJoin(
                db.Children.Echoed(),
                x => x.Cells[ParentKey].Value.TextValue,
                x => x.Cells[ChildKey].Value.TextValue,
                (parent, children) => new ValueTuple<string, int>(
                    parent.Cells[ParentId].Value.TextValue,
                    children.Count()
                )
            )
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsTheCollectionOfASelectMany()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.SelectMany(
                parent =>
                    db.Children.Echoed()
                        .Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        ),
                (parent, child) => new ValueTuple<string, string>(
                    parent.Cells[ParentId].Value.TextValue,
                    child.Cells[ChildId].Value.TextValue
                )
            )
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfAsASideOfAConcatenation()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Concat(db.Parents.Echoed())
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfInAProjection()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Select(parent => new ValueTuple<string, int>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Echoed()
                    .Count(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
            ))
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfInASortKey()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.OrderBy(parent =>
                    db.Children.Echoed()
                        .Count(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .ThenBy(x => x.Cells[ParentId].Value.TextValue)
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfOverAGroup()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.GroupBy(x => x.Cells[ParentKey].Value.TextValue)
                .Select(group => new ValueTuple<string, int>(
                    group.Key,
                    db.Children.Echoed()
                        .Count(child => child.Cells[ChildKey].Value.TextValue == group.Key)
                ))
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfInAMembershipTest()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Echoed()
                        .Select(child => child.Cells[ChildKey].Value.TextValue)
                        .Contains(parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryIsTheCallItselfUnderAnAggregate()
    {
        ExecutionTables enumerated = ExecutionTables.Enumerated(_fixture);
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        Assert.Equal(5, enumerated.Parents.Echoed().Count());
        _ = Assert.Throws<NotSupportedException>(() => db.Parents.Echoed().Count());
        _ = Assert.Throws<NotSupportedException>(() =>
            db.Parents.Echoed().FirstOrDefault(x => x.Cells[ParentKey].Value.TextValue == "a")
        );
        Assert.Empty(connection.Statements);
    }

    [Fact]
    public void RefusesAnOperatorReturnedByAMethodThatIsInlined()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    Wrapped(db.Children)
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorHeldByACapturedQuery()
    {
        Enumeration.AssertNotSupported(db =>
        {
            IQueryable<IRow> children = db.Children.Echoed()
                .Where(x => x.Cells[ChildKey].Value.TextValue != "");
            return db.Parents.Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Fact]
    public void RefusesAnOperatorThatRewritesItsCallToAnotherOperator()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Rewritten()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesOperatorsThatCallEachOther()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Pinged()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
        Enumeration.AssertNotSupported(db =>
            db.Parents.Ponged().Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAnOperatorWhoseQueryHoldsTwoCallsOfItself()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Doubled()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void RefusesAQueryWhoseTreeIsAConversionOfItself()
    {
        Enumeration.AssertNotSupported(db =>
        {
            ConvertedRows children = new ConvertedRows(db.Children);
            return db.Parents.Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Fact]
    public void RefusesAPropertyWhoseQueryIsAReadOfTheProperty()
    {
        Enumeration.AssertNotSupported(db =>
        {
            NamedRows children = new NamedRows(db.Children);
            return db.Parents.Where(parent =>
                    children.Rows.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Fact]
    public void RefusesAMethodWhoseQueryIsACallOfItselfHoldingNoQuery()
    {
        Enumeration.AssertNotSupported(db =>
        {
            NamedRows children = new NamedRows(db.Children);
            return db.Parents.Where(parent =>
                    children
                        .Read()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Fact]
    public void RefusesAnOperatorThatRefusesTheProviderWhenCalled()
    {
        Enumeration.AssertNotSupported(db =>
            db.Parents.Where(parent =>
                    db.Children.Refusing()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void ThrowsWhatAFailingCallThrows()
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        _ = Assert.Throws<InvalidOperationException>(() =>
            db.Parents.Where(parent =>
                    db.Children.Failing()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .ToList()
        );
    }

    [Theory]
    [InlineData(nameof(QueryOperators.Echoed), false)]
    [InlineData(nameof(QueryOperators.Echoed), true)]
    [InlineData(nameof(QueryOperators.Rewritten), false)]
    [InlineData(nameof(QueryOperators.Rewritten), true)]
    [InlineData(nameof(QueryOperators.Pinged), false)]
    [InlineData(nameof(QueryOperators.Pinged), true)]
    [InlineData(nameof(QueryOperators.Doubled), false)]
    [InlineData(nameof(QueryOperators.Doubled), true)]
    [InlineData(nameof(ConvertedRows), true)]
    [InlineData(nameof(NamedRows), true)]
    [InlineData(nameof(NamedRows.Read), true)]
    public async Task EndsAQueryOverAnOperatorThatInlinesToItself(string name, bool nested)
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        Exception? thrown = await Task.Run<Exception?>(() =>
                Record.Exception(() => Operated(db, name, nested).ToList())
            )
            .WaitAsync(TimeSpan.FromMinutes(1));

        _ = Assert.IsType<NotSupportedException>(thrown);
    }

    [Fact]
    public void InlinesAMethodReturningAnotherQueryOfTheSameMethodsArguments()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    Keyed(Keyed(db.Children))
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void InlinesAMethodWhoseQueryCallsItselfOnOtherArguments(int depth)
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    Matched(db, depth)
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void InlinesAMethodReturningATableInsideAMethodReturningAQuery()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    Keyed(Tabled(db))
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    /// <summary>
    /// A query over a source built by an operator that inlines to itself, as the
    /// whole query or nested in a lambda.
    /// </summary>
    private IQueryable<string> Operated(ExecutionTables db, string name, bool nested)
    {
        ConvertedRows converted = new ConvertedRows(db.Children);
        NamedRows named = new NamedRows(db.Children);
        if (!nested)
        {
            IQueryable<IRow> source = name switch
            {
                nameof(QueryOperators.Echoed) => db.Parents.Echoed(),
                nameof(QueryOperators.Rewritten) => db.Parents.Rewritten(),
                nameof(QueryOperators.Pinged) => db.Parents.Pinged(),
                _ => db.Parents.Doubled(),
            };
            return source.Select(x => x.Cells[ParentId].Value.TextValue);
        }
        return name switch
        {
            nameof(QueryOperators.Echoed) => db.Parents.Where(parent =>
                    db.Children.Echoed().Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            nameof(QueryOperators.Rewritten) => db.Parents.Where(parent =>
                    db.Children.Rewritten().Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            nameof(QueryOperators.Pinged) => db.Parents.Where(parent =>
                    db.Children.Pinged().Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            nameof(QueryOperators.Doubled) => db.Parents.Where(parent =>
                    db.Children.Doubled().Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            nameof(ConvertedRows) => db.Parents.Where(parent =>
                    converted.Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue)
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            nameof(NamedRows.Read) => db.Parents.Where(parent =>
                    named
                        .Read()
                        .Any(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue),
            _ => db.Parents.Where(parent => named.Rows.Any(child => child.Cells[ChildKey].Value.TextValue == parent.Cells[ParentKey].Value.TextValue))
                .Select(x => x.Cells[ParentId].Value.TextValue),
        };
    }

    /// <summary>
    /// A method that returns the query of an operator over its argument.
    /// </summary>
    private static IQueryable<IRow> Wrapped(IQueryable<IRow> rows)
    {
        return rows.Echoed();
    }

    /// <summary>
    /// A method that returns a query over its argument, called on its own result.
    /// </summary>
    private IQueryable<IRow> Keyed(IQueryable<IRow> rows)
    {
        return rows.Where(x => x.Cells[ChildKey].Value.TextValue != null);
    }

    /// <summary>
    /// A method that returns a stored table.
    /// </summary>
    private static IQueryable<IRow> Tabled(ExecutionTables db)
    {
        return db.Children;
    }

    /// <summary>
    /// The children whose key a child of the level below has, down to the depth:
    /// a query whose tree calls this method again, with a smaller depth.
    /// </summary>
    private IQueryable<IRow> Matched(ExecutionTables db, int depth)
    {
        return depth == 0
            ? db.Children
            : db.Children.Where(child =>
                Matched(db, depth - 1)
                    .Any(below =>
                        below.Cells[ChildKey].Value.TextValue
                        == child.Cells[ChildKey].Value.TextValue
                    )
            );
    }
}

/// <summary>
/// Query operators of a library's own. Over LINQ to Objects each returns its
/// source; over another provider <see cref="Echoed"/> builds the query whose tree
/// is its own call, <see cref="Rewritten"/> one whose tree is a call of
/// <see cref="Echoed"/>, <see cref="Pinged"/> and <see cref="Ponged"/> ones whose
/// trees are calls of each other, <see cref="Doubled"/> one whose tree is its call
/// over its call, <see cref="Refusing"/> refuses it, and <see cref="Failing"/>
/// always fails.
/// </summary>
internal static class QueryOperators
{
    public static IQueryable<T> Echoed<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(Called(nameof(Echoed), source.Expression));
    }

    public static IQueryable<T> Rewritten<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(Called(nameof(Echoed), source.Expression));
    }

    public static IQueryable<T> Pinged<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(Called(nameof(Ponged), source.Expression));
    }

    public static IQueryable<T> Ponged<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(Called(nameof(Pinged), source.Expression));
    }

    public static IQueryable<T> Doubled<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(
                Called(nameof(Doubled), Called(nameof(Doubled), source.Expression))
            );
    }

    public static IQueryable<T> Refusing<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : throw new NotSupportedException(
                $"{nameof(Refusing)} runs over LINQ to Objects only."
            );
    }

    public static IQueryable<T> Failing<T>(this IQueryable<T> source)
    {
        throw new InvalidOperationException(
            $"{nameof(Failing)} always fails, over {source.ElementType.Name} too."
        );
    }

    private static MethodCallExpression Called(string name, Expression source)
    {
        Type element = source.Type.GetGenericArguments()[0];
        MethodInfo method = typeof(QueryOperators)
            .GetMethod(name)!
            .MakeGenericMethod(element);
        return Expression.Call(method, source);
    }
}

/// <summary>
/// Rows whose tree, over a provider other than LINQ to Objects, is a conversion of
/// themselves.
/// </summary>
internal sealed class ConvertedRows(IQueryable<IRow> rows) : IQueryable<IRow>
{
    private readonly IQueryable<IRow> _rows = rows;

    public Type ElementType => typeof(IRow);

    public Expression Expression =>
        _rows.Provider is EnumerableQuery
            ? _rows.Expression
            : Expression.Convert(Expression.Constant(this), typeof(IQueryable<IRow>));

    public IQueryProvider Provider => _rows.Provider;

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Rows held by a property whose query, over a provider other than LINQ to
/// Objects, is a read of the property, and returned by a method whose query is a
/// call of the method.
/// </summary>
internal sealed class NamedRows(IQueryable<IRow> rows)
{
    private readonly IQueryable<IRow> _rows = rows;

    public IQueryable<IRow> Rows =>
        _rows.Provider is EnumerableQuery
            ? _rows
            : _rows.Provider.CreateQuery<IRow>(
                Expression.Property(Expression.Constant(this), nameof(Rows))
            );

    /// <summary>
    /// The rows, whose query over a provider other than LINQ to Objects is a call of
    /// this method, which holds no query.
    /// </summary>
    public IQueryable<IRow> Read()
    {
        return _rows.Provider is EnumerableQuery
            ? _rows
            : _rows.Provider.CreateQuery<IRow>(
                Expression.Call(
                    Expression.Constant(this),
                    typeof(NamedRows).GetMethod(nameof(Read))!
                )
            );
    }
}
