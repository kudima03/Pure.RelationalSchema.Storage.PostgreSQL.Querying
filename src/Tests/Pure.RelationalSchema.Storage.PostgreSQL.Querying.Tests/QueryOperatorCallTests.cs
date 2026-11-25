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
/// rather than evaluating the call again without end; a method returning another
/// query is still inlined.
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
}

/// <summary>
/// Query operators of a library's own. Over LINQ to Objects each computes its
/// rows (all of them); over another provider <see cref="Echoed"/> builds the query
/// whose tree is its own call, <see cref="Refusing"/> refuses it, and
/// <see cref="Failing"/> always fails.
/// </summary>
internal static class QueryOperators
{
    private static readonly MethodInfo EchoedMethod = typeof(QueryOperators).GetMethod(
        nameof(Echoed)
    )!;

    public static IQueryable<T> Echoed<T>(this IQueryable<T> source)
    {
        return source.Provider is EnumerableQuery
            ? source
            : source.Provider.CreateQuery<T>(
                Expression.Call(EchoedMethod.MakeGenericMethod(typeof(T)), source.Expression)
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
}
