using System.Data;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads a row of a table from its columns, which the result holds in the order of
/// the table's columns, as cells of text: NULL is empty text. An optional row, the
/// element of a <c>DefaultIfEmpty</c> a left join reads as NULL columns, is null
/// where every column is NULL.
/// </summary>
internal sealed record RowReader : IResultReader
{
    private readonly IReadOnlyList<IColumn> _columns;

    private readonly bool _optional;

    public RowReader(ITable table)
        : this(table, false) { }

    public RowReader(ITable table, bool optional)
    {
        _columns = [.. table.Columns];
        _optional = optional;
    }

    public int Width => _columns.Count;

    public object? Read(IDataRecord record, int offset)
    {
        return Row(record, offset);
    }

    public IRow? Row(IDataRecord record, int offset)
    {
        object[] values =
        [
            .. Enumerable.Range(offset, _columns.Count).Select(record.GetValue),
        ];
        if (_optional && values.All(x => x is DBNull))
        {
            return null;
        }
        IReadOnlyList<(IColumn Column, string Text)> cells =
        [
            .. _columns.Select(
                (column, index) => (column, new CellText(values[index]).TextValue)
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
