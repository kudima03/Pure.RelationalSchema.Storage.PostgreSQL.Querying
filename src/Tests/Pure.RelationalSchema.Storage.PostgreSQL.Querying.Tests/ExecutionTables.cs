using System.Data;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The tables of <see cref="ExecutionFixture"/> a query runs over: either the stored
/// tables of a connection, whose queries are translated, or their rows enumerated
/// once into memory, whose queries run with LINQ to Objects.
/// </summary>
internal sealed record ExecutionTables
{
    private readonly ExecutionFixture _fixture;

    private readonly Func<ISchema, ITable, IQueryable<IRow>> _table;

    private ExecutionTables(
        ExecutionFixture fixture,
        Func<ISchema, ITable, IQueryable<IRow>> table
    )
    {
        _fixture = fixture;
        _table = table;
    }

    public IQueryable<IRow> Values => _table(_fixture.Store, _fixture.Values);

    public IQueryable<IRow> Parents => _table(_fixture.Store, _fixture.Parents);

    public IQueryable<IRow> Children => _table(_fixture.Store, _fixture.Children);

    public IQueryable<IRow> Extras => _table(_fixture.Store, _fixture.Extras);

    public IQueryable<IRow> Entries => _table(_fixture.Ledger, _fixture.Entries);

    /// <summary>
    /// The stored tables of the connection.
    /// </summary>
    public static ExecutionTables Stored(
        ExecutionFixture fixture,
        IDbConnection connection
    )
    {
        return new ExecutionTables(
            fixture,
            (schema, table) =>
                new PostgreSqlStoredSchemaDataSet(schema, connection)[table]
        );
    }

    /// <summary>
    /// The rows of the stored tables, each table enumerated once, so that a query
    /// reading a table twice reads the same rows.
    /// </summary>
    public static ExecutionTables Enumerated(ExecutionFixture fixture)
    {
        Dictionary<ITable, IRow[]> rows = new Dictionary<ITable, IRow[]>(
            ReferenceEqualityComparer.Instance
        );
        return new ExecutionTables(
            fixture,
            (schema, table) =>
            {
                if (!rows.TryGetValue(table, out IRow[]? read))
                {
                    read =
                    [
                        .. new PostgreSqlStoredSchemaDataSet(schema, fixture.Connection)[
                            table
                        ].AsEnumerable(),
                    ];
                    rows.Add(table, read);
                }
                return new EnumerableQuery<IRow>(read);
            }
        );
    }
}
