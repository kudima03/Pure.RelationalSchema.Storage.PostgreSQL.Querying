using System.Globalization;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A row of the <c>Values</c> table read as its types, the keys of every type that
/// windows partition and order by: the id, a flag, signed and unsigned numbers, the
/// text, a uuid and the text of the instant.
/// </summary>
internal sealed record EdgeItem(
    long Id,
    bool? Flag,
    int? Number,
    long? Big,
    string Text,
    Guid? Code,
    ushort? Small,
    ulong? Huge,
    string MomentText
);

/// <summary>
/// A row of the <c>Extras</c> table read as its types: a date, a time of day, a
/// double and the text of a <c>determined_hash</c>.
/// </summary>
internal sealed record ExtraItem(
    long Id,
    DateOnly? Day,
    TimeOnly? Clock,
    double? Measure,
    string Digest
);

/// <summary>
/// The sources of the window edge tests over <see cref="ExecutionFixture"/>, and
/// the window over a source built as a provider builds it: a call of
/// <c>PostgreSqlWindows.Over</c> created by the provider of the stored tables,
/// whatever the source is, stored rows or an in-memory sequence.
/// </summary>
internal sealed record WindowEdgeSources
{
    private readonly ExecutionFixture _fixture;

    public WindowEdgeSources(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The window over the source, created by the provider of the stored tables.
    /// </summary>
    public static IQueryable<TResult> Over<TSource, TResult>(
        ExecutionTables db,
        IQueryable<TSource> source,
        Expression<Func<TSource, object?>>? partitionBy,
        Expression<Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>>? orderBy,
        PostgreSqlFrame? frame,
        Expression<Func<TSource, PostgreSqlWindow<TSource>, TResult>> selector
    )
    {
        return db.Values.Provider.CreateQuery<TResult>(
            MarkerCalls.OverCall(source.Expression, partitionBy, orderBy, frame, selector)
        );
    }

    /// <summary>
    /// The rows of <c>Values</c> as <see cref="EdgeItem"/>s.
    /// </summary>
    public IQueryable<EdgeItem> Items(ExecutionTables db)
    {
        ExecutionFixture f = _fixture;
        return db.Values.Select(x => new EdgeItem(
            long.Parse(x.Cells[f.ValueId].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Flag].Value.TextValue)
                ? null
                : bool.Parse(x.Cells[f.Flag].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Number].Value.TextValue)
                ? null
                : int.Parse(x.Cells[f.Number].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Big].Value.TextValue)
                ? null
                : long.Parse(x.Cells[f.Big].Value.TextValue),
            x.Cells[f.Text].Value.TextValue,
            string.IsNullOrEmpty(x.Cells[f.Code].Value.TextValue)
                ? null
                : Guid.Parse(x.Cells[f.Code].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Small].Value.TextValue)
                ? null
                : ushort.Parse(x.Cells[f.Small].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Huge].Value.TextValue)
                ? null
                : ulong.Parse(x.Cells[f.Huge].Value.TextValue),
            x.Cells[f.Moment].Value.TextValue
        ));
    }

    /// <summary>
    /// The rows of <c>Extras</c> as <see cref="ExtraItem"/>s.
    /// </summary>
    public IQueryable<ExtraItem> Extras(ExecutionTables db)
    {
        ExecutionFixture f = _fixture;
        return db.Extras.Select(x => new ExtraItem(
            long.Parse(x.Cells[f.ExtraId].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[f.Day].Value.TextValue)
                ? null
                : DateOnly.Parse(
                    x.Cells[f.Day].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            string.IsNullOrEmpty(x.Cells[f.Clock].Value.TextValue)
                ? null
                : TimeOnly.Parse(
                    x.Cells[f.Clock].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            string.IsNullOrEmpty(x.Cells[f.Measure].Value.TextValue)
                ? null
                : double.Parse(
                    x.Cells[f.Measure].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            x.Cells[f.Digest].Value.TextValue
        ));
    }

    /// <summary>
    /// The results of the window computed in memory for each row of the source, in
    /// the order of the source, as PostgreSQL returns one result per row of the
    /// source of <c>Over</c> in its order.
    /// </summary>
    public static IEnumerable<TResult> InSourceOrder<TSource, TResult>(
        IEnumerable<TSource> source,
        Func<TSource, object?> partitionBy,
        Func<IEnumerable<TSource>, IOrderedEnumerable<TSource>>? orderBy,
        Func<TSource, object?> peerKey,
        Func<TSource, InMemoryWindow<TSource>, TResult> selector
    )
    {
        TSource[] rows = [.. source];
        (TSource Row, InMemoryWindow<TSource> Window)[] windows =
        [
            .. InMemoryWindow<TSource>.Over(rows, partitionBy, orderBy, peerKey),
        ];
        return rows.Select(row =>
        {
            (TSource Row, InMemoryWindow<TSource> Window) found = windows.Single(x =>
                Equals(x.Row, row)
            );
            return selector(found.Row, found.Window);
        });
    }
}
