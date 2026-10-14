using System.Collections;
using System.Data;
using System.Linq.Expressions;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

public sealed record PostgreSqlStoredTableDataSet : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    private readonly IAsyncEnumerable<IRow> _rowsAsync;

    public PostgreSqlStoredTableDataSet(
        ITable tableSchema,
        ISchema schema,
        IDbConnection connection
    )
        : this(tableSchema, new HexString(new SchemaHash(schema)), connection) { }

    public PostgreSqlStoredTableDataSet(
        ITable tableSchema,
        IString schemaName,
        IDbConnection connection
    )
        : this(
            tableSchema,
            new RowsEnumerable(connection, schemaName, tableSchema).AsQueryable(),
            new RowsAsyncEnumerable(connection, schemaName, tableSchema),
            connection,
            schemaName
        )
    { }

    private PostgreSqlStoredTableDataSet(
        ITable tableSchema,
        IQueryable<IRow> rows,
        IAsyncEnumerable<IRow> rowsAsync,
        IDbConnection connection,
        IString schemaName
    )
    {
        _rows = rows;
        _rowsAsync = rowsAsync;
        TableSchema = tableSchema;
        Connection = connection;
        SchemaName = schemaName;
    }

    public IString SchemaName { get; }

    public IDbConnection Connection { get; }

    public ITable TableSchema { get; }

    public Type ElementType => _rows.ElementType;

    public Expression Expression => _rows.Expression;

    public IQueryProvider Provider => _rows.Provider;

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return _rowsAsync.GetAsyncEnumerator(cancellationToken);
    }
}
