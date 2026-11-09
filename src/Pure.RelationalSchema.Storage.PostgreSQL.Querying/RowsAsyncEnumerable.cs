using System.Data;
using System.Data.Common;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The rows of a stored table, read asynchronously with its
/// <see cref="SelectAllStatement"/> when the connection is a
/// <see cref="DbConnection"/>, otherwise as <see cref="RowsEnumerable"/> reads them.
/// Every row is read before the first is returned, so that the connection is free
/// for the queries run while the rows are enumerated.
/// </summary>
internal sealed record RowsAsyncEnumerable : IAsyncEnumerable<IRow>
{
    private readonly IDbConnection _connection;

    private readonly ITable _table;

    private readonly IString _schemaName;

    public RowsAsyncEnumerable(IDbConnection connection, IString schemaName, ITable table)
    {
        _connection = connection;
        _table = table;
        _schemaName = schemaName;
    }

    public async IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<IRow> rows = _connection is DbConnection connection
            ? await ReadAsync(connection, cancellationToken)
            : [.. new RowsEnumerable(_connection, _schemaName, _table)];
        foreach (IRow row in rows)
        {
            yield return row;
        }
    }

    private async Task<IReadOnlyList<IRow>> ReadAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = new SelectAllStatement(_table, _schemaName).TextValue;
        await using DbDataReader reader = await command.ExecuteReaderAsync(
            cancellationToken
        );
        RowReader rows = new(_table);
        List<IRow> read = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            read.Add(rows.Row(reader, 0)!);
        }
        return read;
    }
}
