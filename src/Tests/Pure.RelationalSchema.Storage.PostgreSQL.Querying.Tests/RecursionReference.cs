using Npgsql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// <c>WITH RECURSIVE r AS (seed UNION ALL step(r))</c> computed in memory, as the
/// translator renders <c>PostgreSqlRecursion.WithRecursive</c>: the rows of the
/// seed are level 0, each level is the step over the rows of the level before only
/// (PostgreSQL's working table), and the levels end at the first empty one; every
/// row of every level is kept, duplicates included. With a maximum depth, a row of
/// a level deeper than it fails as the statement does, with a
/// <see cref="PostgresException"/>.
/// </summary>
internal static class RecursionReference
{
    /// <summary>
    /// The level past which a walk is taken not to end, which no test reaches.
    /// </summary>
    private const int Endless = 10_000;

    public static List<T> Levels<T>(
        IEnumerable<T> seed,
        Func<IQueryable<T>, IQueryable<T>> step
    )
    {
        return Walk(seed, step, null);
    }

    public static List<T> Levels<T>(
        IEnumerable<T> seed,
        Func<IQueryable<T>, IQueryable<T>> step,
        long maxDepth
    )
    {
        return Walk(seed, step, maxDepth);
    }

    /// <summary>
    /// The message of the failure of a row deeper than the maximum depth.
    /// </summary>
    public static string DeeperThan(long maxDepth)
    {
        return $"WITH RECURSIVE: a row deeper than the maximum depth of {maxDepth}";
    }

    private static List<T> Walk<T>(
        IEnumerable<T> seed,
        Func<IQueryable<T>, IQueryable<T>> step,
        long? maxDepth
    )
    {
        List<T> rows = [];
        T[] level = [.. seed];
        long depth = 0;
        while (level.Length > 0)
        {
            if (maxDepth is long limit && depth > limit)
            {
                throw new PostgresException(
                    "invalid input syntax for type bigint: \"" + DeeperThan(limit) + "\"",
                    "ERROR",
                    "ERROR",
                    "22P02"
                );
            }
            if (depth > Endless)
            {
                throw new InvalidOperationException("The walk does not end.");
            }
            rows.AddRange(level);
            level = [.. step(level.AsQueryable())];
            depth++;
        }
        return rows;
    }
}
