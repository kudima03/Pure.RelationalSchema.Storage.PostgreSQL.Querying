using System.Data;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record RowReader : IResultReader
{
    private readonly IReadOnlyList<IColumn> _columns;

    public RowReader(ITable table)
    {
        _columns = [.. table.Columns];
    }

    public int Width => _columns.Count;

    public object? Read(IDataRecord record, int offset)
    {
        IReadOnlyList<(IColumn Column, string Text)> cells =
        [
            .. _columns.Select(
                (column, index) =>
                    (column, new CellText(record.GetValue(offset + index)).TextValue)
            ),
        ];
        return new Row(
            new Dictionary<(IColumn Column, string Text), IColumn, ICell>(
                cells,
                x => x.Column,
                x => new Cell(new String(x.Text)),
                x => new ColumnHash(x)
            )
        );
    }
}
