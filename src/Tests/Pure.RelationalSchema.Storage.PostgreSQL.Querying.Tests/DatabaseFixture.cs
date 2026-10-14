using System.Data;
using Npgsql;
using Pure.Primitives.Bool;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Testcontainers.PostgreSql;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Column = Column.Column;
using ForeignKey = ForeignKey.ForeignKey;
using Index = Index.Index;
using Schema = Schema.Schema;
using Table = Table.Table;

public sealed record DatabaseFixture : IDisposable
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
        };

    private readonly PostgreSqlContainer _postgres;

    public IDbConnection Connection { get; }

    public ISchema Schema { get; }

    public DatabaseFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        IReadOnlyCollection<IColumn> columns1 =
        [
            new Column(new String("Column1"), new BoolColumnType()),
            //new Column(new String("Column2"), new DateColumnType()),
            new Column(new String("Column3"), new DateTimeColumnType()),
            new Column(new String("Column4"), new IntColumnType()),
            new Column(new String("Column5"), new LongColumnType()),
        ];

        IReadOnlyCollection<IColumn> columns2 =
        [
            new Column(new String("Column6"), new StringColumnType()),
            //new Column(new String("Column7"), new TimeColumnType()),
            new Column(new String("Column8"), new UIntColumnType()),
            new Column(new String("Column9"), new ULongColumnType()),
            new Column(new String("Column10"), new UShortColumnType()),
            new Column(new String("Column11"), new DeterminedHashColumnType()),
        ];

        ITable table1 = new Table(
            new String("Test"),
            columns1,
            [
                new Index(new False(), columns1.Skip(1).Take(1)),
                new Index(new False(), columns1.Skip(2).Take(2)),
            ]
        );

        ITable table2 = new Table(
            new String("Test"),
            columns2,
            [
                new Index(new True(), columns2.Take(1)),
                new Index(new True(), columns2.Skip(1).Take(1)),
                new Index(new False(), columns2.Skip(2).Take(2)),
            ]
        );

        _ = new ForeignKey(
            table1,
            [table1.Columns.First()],
            table2,
            [table2.Columns.First()]
        );

        _ = new ForeignKey(
            table1,
            [table1.Columns.Skip(1).First()],
            table2,
            [table2.Columns.Skip(1).First()]
        );

        ISchema schema = new Schema(new String("Test"), [table1, table2], []);

        CreateSchema(schema);

        Schema = schema;
    }

    private void CreateSchema(ISchema schema)
    {
        using IDbCommand command = Connection.CreateCommand();

        command.CommandText = $"CREATE SCHEMA IF NOT EXISTS {Quote(new SchemaHash(schema))};";

        _ = command.ExecuteNonQuery();

        foreach (ITable table in schema.Tables)
        {
            string columns = string.Join(
                ", ",
                table.Columns.Select(x =>
                    $"{Quote(new ColumnHash(x))} {PostgreSqlTypes[x.Type.GetType()]} NOT NULL"
                )
            );

            command.CommandText =
                $"CREATE TABLE IF NOT EXISTS {Quote(new SchemaHash(schema))}."
                + $"{Quote(new TableHash(table))} ({columns});";

            _ = command.ExecuteNonQuery();
        }
    }

    private static string Quote(IEnumerable<byte> hash)
    {
        string identifier = new HexString(hash).TextValue;

        return $"\"{identifier[..Math.Min(identifier.Length, MaxIdentifierLength)]}\"";
    }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
