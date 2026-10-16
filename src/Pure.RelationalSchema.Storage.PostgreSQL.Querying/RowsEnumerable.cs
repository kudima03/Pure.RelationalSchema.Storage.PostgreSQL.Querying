using System.Collections;
using System.Data;
using Pure.Collections.Generic;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record RowsEnumerable : IEnumerable<IRow>
{
    private readonly IDbConnection _connection;

    private readonly ITable _table;

    private readonly IString _schemaName;

    public RowsEnumerable(IDbConnection connection, IString schemaName, ITable table)
    {
        _connection = connection;
        _table = table;
        _schemaName = schemaName;
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        IEnumerable<IColumn> columns = [.. _table.Columns];

        IEnumerable<string> columnNames =
        [
            .. _table.Columns.Select(x =>
                new TrimmedHash(new HexString(new ColumnHash(x))).TextValue
            ),
        ];

        using IDbCommand cmd = _connection.CreateCommand();

        cmd.CommandText = new SelectAllStatement(_table, _schemaName).TextValue;

        using IDataReader reader = cmd.ExecuteReader();

        ICollection<IRow> rows = [];

        while (reader.Read())
        {
            IReadOnlyDictionary<string, string> rawCells = columnNames.ToDictionary(
                x => x,
                x => new CellText(reader[x]).TextValue
            );

            IReadOnlyDictionary<IColumn, ICell> cells = new Dictionary<
                IColumn,
                IColumn,
                ICell
            >(
                columns,
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

            rows.Add(new Row(cells));
        }

        return rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
