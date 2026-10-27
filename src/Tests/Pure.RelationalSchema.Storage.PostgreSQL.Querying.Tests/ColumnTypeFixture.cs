using System.Data;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Testcontainers.PostgreSql;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Column = Column.Column;
using Schema = Schema.Schema;
using Table = Table.Table;

/// <summary>
/// A table with a column of each of the types <c>date</c>, <c>time</c>,
/// <c>double</c>, <c>float</c> and <c>determined_hash</c>, holding values whose
/// text PostgreSQL writes differently from .NET.
/// </summary>
public sealed record ColumnTypeFixture : IDisposable
{
    private readonly PostgreSqlContainer _postgres;

    public ColumnTypeFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        Id = new Column(new String("Id"), new LongColumnType());
        Day = new Column(new String("Day"), new DateColumnType());
        Clock = new Column(new String("Clock"), new TimeColumnType());
        Measure = new Column(new String("Measure"), new DoubleColumnType());
        Ratio = new Column(new String("Ratio"), new FloatColumnType());
        Digest = new Column(new String("Digest"), new DeterminedHashColumnType());

        Readings = new Table(
            new String("Readings"),
            [Id, Day, Clock, Measure, Ratio, Digest],
            []
        );

        Lab = new Schema(new String("Lab"), [Readings], []);

        TestSchema.Create(Connection, Lab);

        TestSchema.Insert(
            Connection,
            Lab,
            Readings,
            ["1", "'2024-02-29'", "'03:04:05'", "0.1", "0.1", "decode('616263', 'hex')"],
            [
                "2",
                "'0999-12-31'",
                "'23:59:59.5'",
                "1e20",
                "3.5",
                "decode('7a7978', 'hex')",
            ],
            [
                "3",
                "'1970-01-01'",
                "'00:00:00'",
                "-2.5",
                "1e-7",
                "decode('313233', 'hex')",
            ],
            ["4", "'2000-02-29'", "'12:30:00'", "1e15", "1e6", "decode('c3a9', 'hex')"],
            ["5", "'1999-12-31'", "'09:08:07'", "1e17", "1e9", "decode('20', 'hex')"],
            [
                "6",
                "'2024-01-01'",
                "'23:00:00'",
                "-5e16",
                "-2.5e8",
                "decode('e29c93', 'hex')",
            ],
            [
                "7",
                "'0001-01-01'",
                "'10:00:00'",
                "1234567890123456.8",
                "1234567.9",
                "decode('e282ac', 'hex')",
            ]
        );
    }

    public IDbConnection Connection { get; }

    public ISchema Lab { get; }

    public ITable Readings { get; }

    public IColumn Id { get; }

    public IColumn Day { get; }

    public IColumn Clock { get; }

    public IColumn Measure { get; }

    public IColumn Ratio { get; }

    public IColumn Digest { get; }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
