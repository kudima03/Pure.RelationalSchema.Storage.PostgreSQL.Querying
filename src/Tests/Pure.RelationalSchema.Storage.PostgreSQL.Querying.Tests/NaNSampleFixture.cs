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
/// A table of stored <c>double precision</c> cells that hold NaN, both infinities,
/// negative and positive numbers, in two buckets: <c>Samples</c> of the schema
/// <c>Sensors</c>, whose columns hold no NULL.
/// </summary>
public sealed record NaNSampleFixture : IDisposable
{
    private readonly PostgreSqlContainer _postgres;

    public NaNSampleFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        SampleId = new Column(new String("Id"), new LongColumnType());
        Bucket = new Column(new String("Bucket"), new LongColumnType());
        Measure = new Column(new String("Measure"), new DoubleColumnType());

        Samples = new Table(new String("Samples"), [SampleId, Bucket, Measure], []);
        Sensors = new Schema(new String("Sensors"), [Samples], []);

        TestSchema.Create(Connection, Sensors);
        TestSchema.Insert(
            Connection,
            Sensors,
            Samples,
            ["1", "1", "1.5"],
            ["2", "1", "'NaN'"],
            ["3", "1", "2.5"],
            ["4", "2", "'-Infinity'"],
            ["5", "2", "'Infinity'"],
            ["6", "2", "'NaN'"],
            ["7", "2", "-0.5"],
            ["8", "1", "3"]
        );
    }

    public IDbConnection Connection { get; }

    public ISchema Sensors { get; }

    public ITable Samples { get; }

    public IColumn SampleId { get; }

    public IColumn Bucket { get; }

    public IColumn Measure { get; }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
