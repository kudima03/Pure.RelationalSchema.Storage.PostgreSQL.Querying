using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Pure.Collections.Generic;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

public sealed record PostgreSqlStoredSchemaDataSet : IPostgreSqlStoredSchemaDataSet
{
    private readonly IReadOnlyDictionary<ITable, IStoredTableDataSet> _storedDatasets;

    public PostgreSqlStoredSchemaDataSet(ISchema schema, IDbConnection connection)
        : this(
            schema,
            new Dictionary<ITable, ITable, IStoredTableDataSet>(
                schema.Tables,
                table => table,
                table => new PostgreSqlStoredTableDataSet(
                    table,
                    new TrimmedHash(new HexString(new SchemaHash(schema))),
                    connection
                ),
                table => new TableHash(table)
            ),
            connection
        )
    { }

    private PostgreSqlStoredSchemaDataSet(
        ISchema schema,
        IReadOnlyDictionary<ITable, IStoredTableDataSet> storedDatasets,
        IDbConnection connection
    )
    {
        _storedDatasets = storedDatasets;
        Connection = connection;
        Schema = schema;
    }

    public IDbConnection Connection { get; }

    public IStoredTableDataSet this[ITable key] => _storedDatasets[key];

    public IEnumerable<ITable> Keys => _storedDatasets.Keys;

    public IEnumerable<IStoredTableDataSet> Values => _storedDatasets.Values;

    public ISchema Schema { get; }

    public int Count => _storedDatasets.Count;

    public IEnumerator<KeyValuePair<ITable, IStoredTableDataSet>> GetEnumerator()
    {
        return _storedDatasets.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool ContainsKey(ITable key)
    {
        return _storedDatasets.ContainsKey(key);
    }

    public bool TryGetValue(
        ITable key,
        [MaybeNullWhen(false)] out IStoredTableDataSet value
    )
    {
        return _storedDatasets.TryGetValue(key, out value);
    }
}
