using System.Linq.Expressions;
using System.Reflection;
using Linq.Expressions.Translation.PostgreSql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Queries over the PostgreSQL markers of the translator, which throw when called,
/// built as the tree a provider or a rewriter builds: a call of the marker over the
/// expression of the source, created by the provider of the source.
/// </summary>
internal static class MarkerCalls
{
    public static IQueryable<TResult> Over<TSource, TResult>(
        IQueryable<TSource> source,
        Expression<Func<TSource, object?>>? partitionBy,
        Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>? orderBy,
        PostgreSqlFrame? frame,
        Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>> selector
    )
    {
        return source.Provider.CreateQuery<TResult>(
            OverCall(source.Expression, partitionBy, orderBy, frame, selector)
        );
    }

    public static MethodCallExpression OverCall<TSource, TResult>(
        Expression source,
        Expression<Func<TSource, object?>>? partitionBy,
        Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>? orderBy,
        PostgreSqlFrame? frame,
        Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>> selector
    )
    {
        MethodInfo method = new Func<
            IQueryable<TSource>,
            Expression<Func<TSource, object?>>?,
            Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>?,
            PostgreSqlFrame?,
            Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>>,
            IQueryable<TResult>
        >(PostgreSqlWindows.Over).Method;
        return Expression.Call(
            method,
            source,
            Quoted(partitionBy),
            Quoted(orderBy),
            Expression.Constant(frame, typeof(PostgreSqlFrame)),
            Expression.Quote(selector)
        );
    }

    public static IQueryable<TSource> WithRecursive<TSource>(
        IQueryable<TSource> seed,
        Expression<Func<IQueryable<TSource>, IQueryable<TSource>>> step
    )
    {
        MethodInfo method = new Func<
            IQueryable<TSource>,
            Expression<Func<IQueryable<TSource>, IQueryable<TSource>>>,
            IQueryable<TSource>
        >(PostgreSqlRecursion.WithRecursive).Method;
        return seed.Provider.CreateQuery<TSource>(
            Expression.Call(method, seed.Expression, Expression.Quote(step))
        );
    }

    public static IQueryable<TSource> WithRecursive<TSource>(
        IQueryable<TSource> seed,
        Expression<Func<IQueryable<TSource>, IQueryable<TSource>>> step,
        long maxDepth
    )
    {
        MethodInfo method = new Func<
            IQueryable<TSource>,
            Expression<Func<IQueryable<TSource>, IQueryable<TSource>>>,
            long,
            IQueryable<TSource>
        >(PostgreSqlRecursion.WithRecursive).Method;
        return seed.Provider.CreateQuery<TSource>(
            Expression.Call(
                method,
                seed.Expression,
                Expression.Quote(step),
                Expression.Constant(maxDepth)
            )
        );
    }

    private static Expression Quoted<T>(Expression<T>? lambda)
    {
        return lambda is null
            ? Expression.Constant(null, typeof(Expression<T>))
            : Expression.Quote(lambda);
    }
}
