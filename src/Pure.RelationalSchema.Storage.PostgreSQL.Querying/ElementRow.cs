using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// A row of a stored table that stands, in the element of a query, for the
/// parameter of a lambda over the table (see <see cref="QueryElement"/>). Its cells
/// are never read.
/// </summary>
internal sealed class ElementRow(ITable table) : IRow
{
    public ITable Table { get; } = table;

    public IReadOnlyDictionary<IColumn, ICell> Cells => throw new NotSupportedException();
}
