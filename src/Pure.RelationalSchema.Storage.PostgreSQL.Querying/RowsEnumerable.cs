using System.Collections;
using System.Data;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The rows of a stored table, read with its <see cref="SelectAllStatement"/>. Every
/// row is read before the first is returned, so that the connection is free for the
/// queries run while the rows are enumerated.
/// </summary>
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
        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = new SelectAllStatement(_table, _schemaName).TextValue;
        using IDataReader reader = command.ExecuteReader();
        RowReader rows = new(_table);
        ICollection<IRow> read = [];
        while (reader.Read())
        {
            read.Add(rows.Row(reader, 0)!);
        }
        return read.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
