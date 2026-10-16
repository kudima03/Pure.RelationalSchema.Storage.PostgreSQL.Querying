using System.Data;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

internal static class TestSchema
{
    private const int MaxIdentifierLength = 63;

    private static readonly IReadOnlyDictionary<Type, string> PostgreSqlTypes =
        new Dictionary<Type, string>
        {
            [typeof(BoolColumnType)] = "boolean",
            [typeof(DateTimeColumnType)] = "timestamp",
            [typeof(IntColumnType)] = "integer",
            [typeof(LongColumnType)] = "bigint",
            [typeof(StringColumnType)] = "text",
            [typeof(UIntColumnType)] = "bigint",
            [typeof(ULongColumnType)] = "numeric",
            [typeof(UShortColumnType)] = "integer",
            [typeof(DeterminedHashColumnType)] = "bytea",
            [typeof(UuidColumnType)] = "uuid",
        };

    public static void Create(IDbConnection connection, ISchema schema)
    {
        using IDbCommand command = connection.CreateCommand();

        command.CommandText =
            $"CREATE SCHEMA IF NOT EXISTS {Quote(new SchemaHash(schema))};";

        _ = command.ExecuteNonQuery();

        foreach (ITable table in schema.Tables)
        {
            CreateTable(connection, $"{Quote(new SchemaHash(schema))}.", table);
        }
    }

    public static void CreateTable(IDbConnection connection, string prefix, ITable table)
    {
        using IDbCommand command = connection.CreateCommand();

        string columns = string.Join(
            ", ",
            table.Columns.Select(x =>
                $"{Quote(new ColumnHash(x))} {PostgreSqlTypes[x.Type.GetType()]} NOT NULL"
            )
        );

        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS {prefix}{Quote(new TableHash(table))} "
            + $"({columns});";

        _ = command.ExecuteNonQuery();
    }

    public static void Insert(
        IDbConnection connection,
        ISchema schema,
        ITable table,
        params string[][] rows
    )
    {
        Insert(connection, $"{Quote(new SchemaHash(schema))}.", table, rows);
    }

    public static void Insert(
        IDbConnection connection,
        string prefix,
        ITable table,
        params string[][] rows
    )
    {
        using IDbCommand command = connection.CreateCommand();

        string columns = string.Join(
            ", ",
            table.Columns.Select(x => Quote(new ColumnHash(x)))
        );

        string values = string.Join(", ", rows.Select(x => $"({string.Join(", ", x)})"));

        command.CommandText =
            $"INSERT INTO {prefix}{Quote(new TableHash(table))} ({columns}) "
            + $"VALUES {values};";

        _ = command.ExecuteNonQuery();
    }

    private static string Quote(IEnumerable<byte> hash)
    {
        string identifier = new HexString(hash).TextValue;

        return $"\"{identifier[..Math.Min(identifier.Length, MaxIdentifierLength)]}\"";
    }
}
