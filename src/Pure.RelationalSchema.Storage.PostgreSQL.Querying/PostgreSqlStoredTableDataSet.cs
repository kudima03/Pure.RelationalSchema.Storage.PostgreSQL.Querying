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
            new RowsEnumerable(connection, schemaName, tableSchema),
            new RowsAsyncEnumerable(connection, schemaName, tableSchema),
            connection,
            schemaName
        )
    { }

    private PostgreSqlStoredTableDataSet(
        ITable tableSchema,
        IEnumerable<IRow> rows,
        IAsyncEnumerable<IRow> rowsAsync,
        IDbConnection connection,
        IString schemaName
    )
    {
        Rows = rows;
        _rowsAsync = rowsAsync;
        TableSchema = tableSchema;
        Connection = connection;
        SchemaName = schemaName;
    }

    public IString SchemaName { get; }

    internal IEnumerable<IRow> Rows { get; }

    public IDbConnection Connection { get; }

    public ITable TableSchema { get; }

    public Type ElementType => typeof(IRow);

    public Expression Expression => Expression.Constant(this, typeof(IQueryable<IRow>));

    public IQueryProvider Provider => new PostgreSqlQueryProvider();

    public IEnumerator<IRow> GetEnumerator()
    {
        return Rows.GetEnumerator();
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

    public override string ToString()
    {
        throw new NotSupportedException();
    }
}
