using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Pure.Collections.Generic;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

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
        DbConnection conn = (DbConnection)_connection;
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = new SelectAllStatement(_table, _schemaName).TextValue;

        await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            IReadOnlyDictionary<string, string> rawCells = _table
                .Columns.Select(x =>
                    new TrimmedHash(new HexString(new ColumnHash(x))).TextValue
                )
                .ToDictionary(
                    x => x,
                    x =>
                        Convert.ToString(reader[x], CultureInfo.InvariantCulture)
                        == Array.Empty<byte>().ToString()
                            ? Encoding.UTF8.GetString(reader[x] as byte[] ?? [])
                            : Convert.ToString(reader[x], CultureInfo.InvariantCulture)
                )!;

            IReadOnlyDictionary<IColumn, ICell> cells = new Dictionary<
                IColumn,
                IColumn,
                ICell
            >(
                _table.Columns,
                x => x,
                x => new Cell(
                    new String(
                        rawCells[
                            new TrimmedHash(new HexString(new ColumnHash(x))).TextValue
                        ]
                    )
                ),
                x => new ColumnHash(x)
            );

            yield return new Row(cells);
        }
    }
}
