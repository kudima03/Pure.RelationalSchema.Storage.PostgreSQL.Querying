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
            [typeof(DateColumnType)] = "date",
            [typeof(DateTimeColumnType)] = "timestamp",
            [typeof(DoubleColumnType)] = "double precision",
            [typeof(FloatColumnType)] = "real",
            [typeof(IntColumnType)] = "integer",
            [typeof(LongColumnType)] = "bigint",
            [typeof(StringColumnType)] = "text",
            [typeof(TimeColumnType)] = "time",
            [typeof(UIntColumnType)] = "bigint",
            [typeof(ULongColumnType)] = "numeric",
            [typeof(UShortColumnType)] = "integer",
            [typeof(DeterminedHashColumnType)] = "bytea",
            [typeof(UuidColumnType)] = "uuid",
        };

    public static void Create(IDbConnection connection, ISchema schema)
    {
        Create(connection, schema, "NOT NULL");
    }

    /// <summary>
    /// Creates the schema with columns that hold NULL, which enumeration reads as
    /// empty text.
    /// </summary>
    public static void CreateNullable(IDbConnection connection, ISchema schema)
    {
        Create(connection, schema, "NULL");
    }

    public static void CreateTable(IDbConnection connection, string prefix, ITable table)
    {
        CreateTable(connection, prefix, table, "NOT NULL");
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

    private static void Create(IDbConnection connection, ISchema schema, string nullity)
    {
        using IDbCommand command = connection.CreateCommand();

        command.CommandText =
            $"CREATE SCHEMA IF NOT EXISTS {Quote(new SchemaHash(schema))};";

        _ = command.ExecuteNonQuery();

        foreach (ITable table in schema.Tables)
        {
            CreateTable(
                connection,
                $"{Quote(new SchemaHash(schema))}.",
                table,
                nullity
            );
        }
    }

    private static void CreateTable(
        IDbConnection connection,
        string prefix,
        ITable table,
        string nullity
    )
    {
        using IDbCommand command = connection.CreateCommand();

        string columns = string.Join(
            ", ",
            table.Columns.Select(x =>
                $"{Quote(new ColumnHash(x))} {PostgreSqlTypes[x.Type.GetType()]} "
                + nullity
            )
        );

        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS {prefix}{Quote(new TableHash(table))} "
            + $"({columns});";

        _ = command.ExecuteNonQuery();
    }

    private static string Quote(IEnumerable<byte> hash)
    {
        string identifier = new HexString(hash).TextValue;

        return $"\"{identifier[..Math.Min(identifier.Length, MaxIdentifierLength)]}\"";
    }
}
